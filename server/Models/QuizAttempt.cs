namespace StudyPilot.Api.Models;

public class QuizAttempt
{
  public int Id { get; set; }
  public DateTime StartedAt { get; set; } = DateTime.UtcNow;
  public DateTime? CompletedAt { get; set; }
  public double? Score { get; set; }

  public int UserId { get; set; }
  public User User { get; set; } = null!;

  public int QuizId { get; set; }
  public Quiz Quiz { get; set; } = null!;

  public List<Answer> Answers { get; set; } = new();
}