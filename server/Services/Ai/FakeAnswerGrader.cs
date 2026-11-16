using System.Text.RegularExpressions;

namespace StudyPilot.Api.Services.Ai;

// Grades open answers by how many of the model answer's words appear in the student's answer.
// Much less accurate than real AI grading, but free, fast and predictable for development and tests.
public partial class FakeAnswerGrader : IAnswerGrader
{
  public Task<List<OpenAnswerGrade>> GradeOpenAnswersAsync(
      IReadOnlyList<OpenAnswerToGrade> answers,
      CancellationToken cancellationToken = default)
  {
    var grades = answers.Select(Grade).ToList();
    return Task.FromResult(grades);
  }

  private static OpenAnswerGrade Grade(OpenAnswerToGrade answer)
  {
    if (string.IsNullOrWhiteSpace(answer.StudentAnswer))
    {
      return new OpenAnswerGrade(0, "לא נכתבה תשובה.");
    }

    var modelWords = GetWords(answer.ModelAnswer);

    if (modelWords.Count == 0)
    {
      return new OpenAnswerGrade(0, "לא ניתן לבדוק את התשובה במצב הדגמה.");
    }

    var studentWords = GetWords(answer.StudentAnswer);
    var overlap = (double)modelWords.Count(studentWords.Contains) / modelWords.Count;

    // Round to one decimal place, so scores look like 0.3 or 0.7 rather than 0.3333.
    var score = Math.Round(Math.Min(overlap * 1.5, 1.0), 1);

    var feedback = score switch
    {
      >= 0.8 => "תשובה טובה, היא כוללת את רוב הרעיונות המרכזיים.",
      >= 0.4 => "תשובה חלקית. כדאי להשוות לתשובה לדוגמה ולראות מה חסר.",
      _ => "התשובה רחוקה מהתשובה לדוגמה. כדאי לחזור על החומר."
    };

    return new OpenAnswerGrade(score, $"{feedback} (בדיקה אוטומטית במצב הדגמה)");
  }

  private static HashSet<string> GetWords(string text) =>
      WordRegex()
          .Matches(text.ToLowerInvariant())
          .Select(match => match.Value)
          .Where(word => word.Length >= 2)
          .ToHashSet();

  [GeneratedRegex(@"[\p{L}\p{N}]+")]
  private static partial Regex WordRegex();
}