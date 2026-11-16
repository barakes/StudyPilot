namespace StudyPilot.Api.Services.Ai;

public interface IAnswerGrader
{
  // Returns one grade per answer, in the same order as the input.
  Task<List<OpenAnswerGrade>> GradeOpenAnswersAsync(
      IReadOnlyList<OpenAnswerToGrade> answers,
      CancellationToken cancellationToken = default);
}

public record OpenAnswerToGrade(string Question, string ModelAnswer, string StudentAnswer);

// Score is between 0 (wrong) and 1 (fully correct).
public record OpenAnswerGrade(double Score, string Feedback);

public class AnswerGradingException : Exception
{
  public AnswerGradingException(string message, Exception? innerException = null)
      : base(message, innerException)
  {
  }
}