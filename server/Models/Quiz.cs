namespace StudyPilot.Api.Models;

public class Quiz
{
  public int Id { get; set; }
  public string Title { get; set; } = string.Empty;
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

  public int DocumentId { get; set; }
  public Document Document { get; set; } = null!;

  public List<Question> Questions { get; set; } = new();
  public List<QuizAttempt> Attempts { get; set; } = new();
}