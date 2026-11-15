namespace StudyPilot.Api.Models;

public class Question
{
  public int Id { get; set; }
  public int Order { get; set; }
  public QuestionType Type { get; set; }
  public string Text { get; set; } = string.Empty;

  public List<string> Options { get; set; } = new();
  public string CorrectAnswer { get; set; } = string.Empty;
  public string Explanation { get; set; } = string.Empty;

  public int QuizId { get; set; }
  public Quiz Quiz { get; set; } = null!;
}