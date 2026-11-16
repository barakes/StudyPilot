using System.ComponentModel.DataAnnotations;

namespace StudyPilot.Api.Dtos;

public class SubmitAttemptRequest
{
  [Required]
  public List<SubmittedAnswer> Answers { get; set; } = new();
}

public class SubmittedAnswer
{
  [Range(1, int.MaxValue)]
  public int QuestionId { get; set; }

  [MaxLength(2000)]
  public string? Answer { get; set; }
}

public record AttemptResultDto(
    int Id,
    int QuizId,
    string QuizTitle,
    DateTime CompletedAt,
    double Score,
    List<AnswerResultDto> Answers);

public record AnswerResultDto(
    int QuestionId,
    int Order,
    string Type,
    string QuestionText,
    List<string> Options,
    string UserAnswer,
    string CorrectAnswer,
    bool IsCorrect,
    double Score,
    string? Feedback,
    string Explanation);

public record AttemptSummaryDto(
    int Id,
    int QuizId,
    string QuizTitle,
    string DocumentTitle,
    DateTime CompletedAt,
    double Score);