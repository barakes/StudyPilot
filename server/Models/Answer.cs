namespace StudyPilot.Api.Models;

public class Answer
{
  public int Id { get; set; }
  public string UserAnswer { get; set; } = string.Empty;
  public bool IsCorrect { get; set; }
  public double Score { get; set; }
  public string? Feedback { get; set; }

  public int AttemptId { get; set; }
  public QuizAttempt Attempt { get; set; } = null!;

  public int QuestionId { get; set; }
  public Question Question { get; set; } = null!;
}