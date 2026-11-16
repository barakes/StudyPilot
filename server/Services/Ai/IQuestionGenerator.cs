using StudyPilot.Api.Models;

namespace StudyPilot.Api.Services.Ai;

public interface IQuestionGenerator
{
  Task<GeneratedQuiz> GenerateAsync(
      string sourceText,
      QuizGenerationOptions options,
      CancellationToken cancellationToken = default);
}

public record QuizGenerationOptions(
    int MultipleChoiceCount,
    int OpenCount,
    IReadOnlyList<string>? QuestionsToAvoid = null);

public record GeneratedQuiz(string Title, List<GeneratedQuestion> Questions);

public record GeneratedQuestion(
    QuestionType Type,
    string Text,
    List<string> Options,
    string CorrectAnswer,
    string Explanation);

public class QuestionGenerationException : Exception
{
  public QuestionGenerationException(string message, Exception? innerException = null)
      : base(message, innerException)
  {
  }
}