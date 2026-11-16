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
[Route("api/[controller]")]
public class QuizzesController : ControllerBase
{
  private const int DailyQuizLimit = 20;
  private const int MaxTitleLength = 200;

  private readonly AppDbContext _db;
  private readonly IQuestionGenerator _questionGenerator;
  private readonly ILogger<QuizzesController> _logger;

  public QuizzesController(
      AppDbContext db,
      IQuestionGenerator questionGenerator,
      ILogger<QuizzesController> logger)
  {
    _db = db;
    _questionGenerator = questionGenerator;
    _logger = logger;
  }

  [HttpGet]
  public async Task<ActionResult<List<QuizSummaryDto>>> GetAll([FromQuery] int? documentId)
  {
    var userId = User.GetUserId();

    var query = _db.Quizzes.Where(q => q.Document.UserId == userId);

    if (documentId is not null)
    {
      query = query.Where(q => q.DocumentId == documentId);
    }

    var quizzes = await query
        .OrderByDescending(q => q.CreatedAt)
        .Select(q => new QuizSummaryDto(
            q.Id,
            q.Title,
            q.CreatedAt,
            q.DocumentId,
            q.Document.Title,
            q.Questions.Count,
            q.Attempts.Count,
            q.Attempts.Max(a => a.Score)))
        .ToListAsync();

    return Ok(quizzes);
  }

  [HttpGet("{id:int}")]
  public async Task<ActionResult<QuizDetailsDto>> GetById(int id)
  {
    var userId = User.GetUserId();

    var quiz = await _db.Quizzes
        .Include(q => q.Document)
        .Include(q => q.Questions)
        .SingleOrDefaultAsync(q => q.Id == id && q.Document.UserId == userId);

    if (quiz is null)
    {
      return NotFound();
    }

    return Ok(ToDetailsDto(quiz));
  }

  [HttpPost]
  public async Task<ActionResult<QuizDetailsDto>> Generate(
      GenerateQuizRequest request,
      CancellationToken cancellationToken)
  {
    if (request.MultipleChoiceCount + request.OpenCount == 0)
    {
      return BadRequest(new { message = "Ask for at least one question." });
    }

    var userId = User.GetUserId();

    var document = await _db.Documents
        .SingleOrDefaultAsync(d => d.Id == request.DocumentId && d.UserId == userId, cancellationToken);

    if (document is null)
    {
      return NotFound(new { message = "Document not found." });
    }

    var since = DateTime.UtcNow.AddDays(-1);
    var recentQuizCount = await _db.Quizzes
        .CountAsync(q => q.Document.UserId == userId && q.CreatedAt >= since, cancellationToken);

    if (recentQuizCount >= DailyQuizLimit)
    {
      return StatusCode(StatusCodes.Status429TooManyRequests, new
      {
        message = $"You can create up to {DailyQuizLimit} quizzes per day. Please try again later."
      });
    }

    GeneratedQuiz generated;

    try
    {
      generated = await _questionGenerator.GenerateAsync(
          document.ExtractedText,
          new QuizGenerationOptions(request.MultipleChoiceCount, request.OpenCount),
          cancellationToken);
    }
    catch (QuestionGenerationException ex)
    {
      _logger.LogWarning(ex, "Question generation failed for document {DocumentId}", document.Id);
      return StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
    }

    var questions = generated.Questions
        .Where(IsValid)
        .Select((question, index) => new Question
        {
          Order = index + 1,
          Type = question.Type,
          Text = question.Text.Trim(),
          Options = question.Options,
          CorrectAnswer = question.CorrectAnswer.Trim(),
          Explanation = question.Explanation.Trim()
        })
        .ToList();

    if (questions.Count == 0)
    {
      return StatusCode(StatusCodes.Status502BadGateway, new
      {
        message = "No valid questions could be generated from this document."
      });
    }

    var title = string.IsNullOrWhiteSpace(generated.Title) ? document.Title : generated.Title.Trim();

    var quiz = new Quiz
    {
      Title = Truncate(title, MaxTitleLength),
      Document = document,
      Questions = questions
    };

    _db.Quizzes.Add(quiz);
    await _db.SaveChangesAsync(cancellationToken);

    return CreatedAtAction(nameof(GetById), new { id = quiz.Id }, ToDetailsDto(quiz));
  }

  [HttpDelete("{id:int}")]
  public async Task<IActionResult> Delete(int id)
  {
    var userId = User.GetUserId();

    var quiz = await _db.Quizzes
        .SingleOrDefaultAsync(q => q.Id == id && q.Document.UserId == userId);

    if (quiz is null)
    {
      return NotFound();
    }

    _db.Quizzes.Remove(quiz);
    await _db.SaveChangesAsync();

    return NoContent();
  }

  // A generated question is only kept if it can actually be answered and graded.
  private static bool IsValid(GeneratedQuestion question)
  {
    if (string.IsNullOrWhiteSpace(question.Text) || string.IsNullOrWhiteSpace(question.CorrectAnswer))
    {
      return false;
    }

    if (question.Type == QuestionType.Open)
    {
      return true;
    }

    return question.Options.Count >= 2 && question.Options.Contains(question.CorrectAnswer);
  }

  private static QuizDetailsDto ToDetailsDto(Quiz quiz) =>
      new(
          quiz.Id,
          quiz.Title,
          quiz.CreatedAt,
          quiz.DocumentId,
          quiz.Document.Title,
          quiz.Questions
              .OrderBy(q => q.Order)
              .Select(q => new QuestionDto(q.Id, q.Order, q.Type.ToString(), q.Text, q.Options))
              .ToList());

  private static string Truncate(string value, int maxLength) =>
      value.Length <= maxLength ? value : value[..maxLength];
}