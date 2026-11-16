using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyPilot.Api.Data;
using StudyPilot.Api.Dtos;
using StudyPilot.Api.Extensions;
using StudyPilot.Api.Models;
using StudyPilot.Api.Services.Ai;

namespace StudyPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public class AttemptsController : ControllerBase
{
  private const int DailyAttemptLimit = 50;

  // An open answer counts as correct from this score and up.
  private const double OpenAnswerPassingScore = 0.7;

  private readonly AppDbContext _db;
  private readonly IAnswerGrader _answerGrader;
  private readonly ILogger<AttemptsController> _logger;

  public AttemptsController(AppDbContext db, IAnswerGrader answerGrader, ILogger<AttemptsController> logger)
  {
    _db = db;
    _answerGrader = answerGrader;
    _logger = logger;
  }

  [HttpPost("quizzes/{quizId:int}/attempts")]
  public async Task<ActionResult<AttemptResultDto>> Submit(
      int quizId,
      SubmitAttemptRequest request,
      CancellationToken cancellationToken)
  {
    var userId = User.GetUserId();

    var quiz = await _db.Quizzes
        .Include(q => q.Questions)
        .SingleOrDefaultAsync(q => q.Id == quizId && q.Document.UserId == userId, cancellationToken);

    if (quiz is null)
    {
      return NotFound();
    }

    var since = DateTime.UtcNow.AddDays(-1);
    var recentAttemptCount = await _db.QuizAttempts
        .CountAsync(a => a.UserId == userId && a.StartedAt >= since, cancellationToken);

    if (recentAttemptCount >= DailyAttemptLimit)
    {
      return StatusCode(StatusCodes.Status429TooManyRequests, new
      {
        message = $"You can submit up to {DailyAttemptLimit} quizzes per day. Please try again later."
      });
    }

    var questionIds = quiz.Questions.Select(q => q.Id).ToHashSet();
    var unknownQuestionIds = request.Answers
        .Select(a => a.QuestionId)
        .Where(id => !questionIds.Contains(id))
        .Distinct()
        .ToList();

    if (unknownQuestionIds.Count > 0)
    {
      return BadRequest(new
      {
        message = $"Questions {string.Join(", ", unknownQuestionIds)} do not belong to this quiz."
      });
    }

    // If the same question was sent twice, keep the first answer.
    var submittedAnswers = request.Answers
        .GroupBy(a => a.QuestionId)
        .ToDictionary(group => group.Key, group => group.First().Answer?.Trim() ?? string.Empty);

    // Every question gets an answer row, so a skipped question counts as an empty answer.
    var answers = quiz.Questions
        .OrderBy(q => q.Order)
        .Select(question => new Answer
        {
          Question = question,
          UserAnswer = submittedAnswers.GetValueOrDefault(question.Id, string.Empty)
        })
        .ToList();

    foreach (var answer in answers.Where(a => a.Question.Type == QuestionType.MultipleChoice))
    {
      answer.IsCorrect = answer.UserAnswer == answer.Question.CorrectAnswer;
      answer.Score = answer.IsCorrect ? 1 : 0;
    }

    var openAnswers = answers.Where(a => a.Question.Type == QuestionType.Open).ToList();

    if (openAnswers.Count > 0)
    {
      List<OpenAnswerGrade> grades;

      try
      {
        grades = await _answerGrader.GradeOpenAnswersAsync(
            openAnswers
                .Select(a => new OpenAnswerToGrade(a.Question.Text, a.Question.CorrectAnswer, a.UserAnswer))
                .ToList(),
            cancellationToken);
      }
      catch (AnswerGradingException ex)
      {
        _logger.LogWarning(ex, "Answer grading failed for quiz {QuizId}", quizId);
        return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
      }

      for (var i = 0; i < openAnswers.Count; i++)
      {
        openAnswers[i].Score = grades[i].Score;
        openAnswers[i].Feedback = grades[i].Feedback;
        openAnswers[i].IsCorrect = grades[i].Score >= OpenAnswerPassingScore;
      }
    }

    var now = DateTime.UtcNow;

    var attempt = new QuizAttempt
    {
      UserId = userId,
      Quiz = quiz,
      StartedAt = now,
      CompletedAt = now,
      Score = Math.Round(answers.Average(a => a.Score) * 100, 1),
      Answers = answers
    };

    _db.QuizAttempts.Add(attempt);
    await _db.SaveChangesAsync(cancellationToken);

    return CreatedAtAction(nameof(GetById), new { id = attempt.Id }, ToResultDto(attempt));
  }

  [HttpGet("attempts")]
  public async Task<ActionResult<List<AttemptSummaryDto>>> GetAll([FromQuery] int? quizId)
  {
    var userId = User.GetUserId();

    var query = _db.QuizAttempts
        .Where(a => a.UserId == userId && a.CompletedAt != null && a.Score != null);

    if (quizId is not null)
    {
      query = query.Where(a => a.QuizId == quizId);
    }

    var attempts = await query
        .OrderByDescending(a => a.CompletedAt)
        .Select(a => new AttemptSummaryDto(
            a.Id,
            a.QuizId,
            a.Quiz.Title,
            a.Quiz.Document.Title,
            a.CompletedAt!.Value,
            a.Score!.Value))
        .ToListAsync();

    return Ok(attempts);
  }

  [HttpGet("attempts/{id:int}")]
  public async Task<ActionResult<AttemptResultDto>> GetById(int id)
  {
    var userId = User.GetUserId();

    var attempt = await _db.QuizAttempts
        .Include(a => a.Quiz)
        .Include(a => a.Answers)
            .ThenInclude(answer => answer.Question)
        .SingleOrDefaultAsync(a => a.Id == id && a.UserId == userId);

    if (attempt is null)
    {
      return NotFound();
    }

    return Ok(ToResultDto(attempt));
  }

  private static AttemptResultDto ToResultDto(QuizAttempt attempt) =>
      new(
          attempt.Id,
          attempt.QuizId,
          attempt.Quiz.Title,
          attempt.CompletedAt ?? attempt.StartedAt,
          attempt.Score ?? 0,
          attempt.Answers
              .OrderBy(a => a.Question.Order)
              .Select(a => new AnswerResultDto(
                  a.QuestionId,
                  a.Question.Order,
                  a.Question.Type.ToString(),
                  a.Question.Text,
                  a.Question.Options,
                  a.UserAnswer,
                  a.Question.CorrectAnswer,
                  a.IsCorrect,
                  a.Score,
                  a.Feedback,
                  a.Question.Explanation))
              .ToList());
}