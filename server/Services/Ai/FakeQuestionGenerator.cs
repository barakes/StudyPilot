using System.Text.RegularExpressions;
using StudyPilot.Api.Models;

namespace StudyPilot.Api.Services.Ai;

// Builds simple questions from the document's own sentences, without calling any AI service.
// Useful for development, tests and demos: free, fast and works offline.
public partial class FakeQuestionGenerator : IQuestionGenerator
{
  private const int MinWordsPerSentence = 6;
  private const int DistractorCount = 3;

  private static readonly string[] FallbackDistractors =
  [
      "אף תשובה אינה נכונה",
        "כל התשובות נכונות",
        "לא ניתן לדעת מהחומר"
  ];

  public async Task<GeneratedQuiz> GenerateAsync(
      string sourceText,
      QuizGenerationOptions options,
      CancellationToken cancellationToken = default)
  {
    // Simulate a short network delay, so the client's loading state can be tested.
    await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

    var sentences = SentenceSplitRegex()
        .Split(sourceText)
        .Select(sentence => sentence.Trim())
        .Where(sentence => sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= MinWordsPerSentence)
        .Distinct()
        .ToList();

    if (sentences.Count == 0)
    {
      throw new QuestionGenerationException("The document does not contain enough text to generate questions.");
    }

    var questions = new List<GeneratedQuestion>();

    for (var i = 0; i < options.MultipleChoiceCount; i++)
    {
      questions.Add(CreateMultipleChoice(sentences, sentences[i % sentences.Count]));
    }

    for (var i = 0; i < options.OpenCount; i++)
    {
      questions.Add(CreateOpen(sentences[(options.MultipleChoiceCount + i) % sentences.Count]));
    }

    return new GeneratedQuiz("בוחן לדוגמה (מצב הדגמה)", questions);
  }

  private static GeneratedQuestion CreateMultipleChoice(List<string> sentences, string sentence)
  {
    var (beginning, correctEnding) = SplitInHalf(sentence);

    var distractors = sentences
        .Where(other => other != sentence)
        .Select(other => SplitInHalf(other).Ending)
        .Where(ending => ending != correctEnding)
        .Distinct()
        .OrderBy(_ => Random.Shared.Next())
        .Take(DistractorCount)
        .ToList();

    foreach (var fallback in FallbackDistractors)
    {
      if (distractors.Count >= DistractorCount)
      {
        break;
      }

      distractors.Add(fallback);
    }

    var options = distractors
        .Append(correctEnding)
        .OrderBy(_ => Random.Shared.Next())
        .ToList();

    return new GeneratedQuestion(
        QuestionType.MultipleChoice,
        $"השלם את המשפט: \"{beginning}...\"",
        options,
        correctEnding,
        $"המשפט המלא מהחומר: \"{sentence}\"");
  }

  private static GeneratedQuestion CreateOpen(string sentence) =>
      new(
          QuestionType.Open,
          $"הסבר במילים שלך את הרעיון הבא: \"{sentence}\"",
          [],
          sentence,
          "בשאלה פתוחה אין ניסוח אחד נכון. התשובה לדוגמה היא המשפט המקורי מהחומר.");

  private static (string Beginning, string Ending) SplitInHalf(string sentence)
  {
    var words = sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var middle = words.Length / 2;

    return (string.Join(' ', words[..middle]), string.Join(' ', words[middle..]));
  }

  [GeneratedRegex(@"(?<=[.!?])\s+|\n+")]
  private static partial Regex SentenceSplitRegex();
}