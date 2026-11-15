namespace StudyPilot.Api.Models;

public class Document
{
  public int Id { get; set; }
  public string Title { get; set; } = string.Empty;
  public string OriginalFileName { get; set; } = string.Empty;
  public string ExtractedText { get; set; } = string.Empty;
  public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

  public int UserId { get; set; }
  public User User { get; set; } = null!;

  public List<Quiz> Quizzes { get; set; } = new();
}