using System.Text.RegularExpressions;

namespace StudyPilot.Api.Services;

// Some PDFs store Hebrew in visual (left-to-right) order, so text comes out reversed.
// This class detects reversed lines and fixes them, and removes headers and footers repeated on every page.
public static partial class HebrewPdfTextFixer
{
  private static readonly HashSet<char> HebrewFinalLetters = ['ך', 'ם', 'ן', 'ף', 'ץ'];

  public static List<List<string>> Fix(List<List<string>> pages)
  {
    var (reversedWords, normalWords) = pages
        .SelectMany(lines => lines)
        .Select(ScoreFinalLetters)
        .Aggregate((0, 0), (total, score) => (total.Item1 + score.Reversed, total.Item2 + score.Normal));

    var documentLooksReversed = reversedWords > normalWords;

    var fixedPages = pages
        .Select(lines => lines.Select(line => FixLine(line, documentLooksReversed)).ToList())
        .ToList();

    return RemoveRepeatedLines(fixedPages);
  }

  // Hebrew final letters only appear at the end of a word, which lets us detect reversed lines.
  public static string FixLine(string line, bool documentLooksReversed = false)
  {
    if (!HebrewLetterRegex().IsMatch(line))
    {
      return line;
    }

    line = MisencodedNunRegex().Replace(line, "נ");

    var (reversed, normal) = ScoreFinalLetters(line);
    var shouldReverse = reversed > normal || (reversed == normal && documentLooksReversed);

    return shouldReverse ? ReverseVisualLine(line) : line;
  }

  private static (int Reversed, int Normal) ScoreFinalLetters(string line)
  {
    var reversed = 0;
    var normal = 0;

    foreach (Match word in HebrewWordRegex().Matches(line))
    {
      if (word.Length < 2)
      {
        continue;
      }

      if (HebrewFinalLetters.Contains(word.Value[0]))
      {
        reversed++;
      }

      if (HebrewFinalLetters.Contains(word.Value[^1]))
      {
        normal++;
      }
    }

    return (reversed, normal);
  }

  private static string ReverseVisualLine(string line)
  {
    var chars = line.ToCharArray();
    Array.Reverse(chars);

    for (var i = 0; i < chars.Length; i++)
    {
      chars[i] = MirrorBracket(chars[i]);
    }

    // Latin text and numbers were already in the right order, so flip them back.
    var result = LtrRunRegex().Replace(new string(chars), match => Reverse(match.Value)).Trim();

    var leadingPunctuation = LeadingPunctuationRegex().Match(result);
    if (leadingPunctuation.Success)
    {
      result = (leadingPunctuation.Groups[2].Value + leadingPunctuation.Groups[1].Value).Trim();
    }

    if (result.EndsWith('•'))
    {
      result = "• " + result[..^1].Trim();
    }

    return result;
  }

  // Lines that appear on more than half of the pages (ignoring page numbers) are headers or footers.
  private static List<List<string>> RemoveRepeatedLines(List<List<string>> pages)
  {
    if (pages.Count < 3)
    {
      return pages;
    }

    var pageCountByLine = new Dictionary<string, int>();

    foreach (var lines in pages)
    {
      foreach (var key in lines.Select(LineKey).Where(key => key.Length > 0).Distinct())
      {
        pageCountByLine[key] = pageCountByLine.GetValueOrDefault(key) + 1;
      }
    }

    var threshold = pages.Count / 2;

    return pages
        .Select(lines => lines
            .Where(line =>
            {
              var key = LineKey(line);
              return key.Length == 0 || pageCountByLine[key] <= threshold;
            })
            .ToList())
        .ToList();
  }

  private static string LineKey(string line) =>
      DigitsRegex().Replace(line, string.Empty).Trim();

  private static string Reverse(string value)
  {
    var chars = value.ToCharArray();
    Array.Reverse(chars);
    return new string(chars);
  }

  private static char MirrorBracket(char c) => c switch
  {
    '(' => ')',
    ')' => '(',
    '[' => ']',
    ']' => '[',
    '{' => '}',
    '}' => '{',
    '<' => '>',
    '>' => '<',
    _ => c
  };

  [GeneratedRegex(@"[\u05D0-\u05EA]")]
  private static partial Regex HebrewLetterRegex();

  [GeneratedRegex(@"[\u05D0-\u05EA]+")]
  private static partial Regex HebrewWordRegex();

  [GeneratedRegex(@"(?<=[\u05D0-\u05EA])\u00AA|\u00AA(?=[\u05D0-\u05EA])")]
  private static partial Regex MisencodedNunRegex();

  [GeneratedRegex(@"[A-Za-z0-9](?:[A-Za-z0-9.,:/%+\-_@#° ]*[A-Za-z0-9%°])?")]
  private static partial Regex LtrRunRegex();

  [GeneratedRegex(@"^([.,:;?!–]+)\s*(.*)$")]
  private static partial Regex LeadingPunctuationRegex();

  [GeneratedRegex(@"\d")]
  private static partial Regex DigitsRegex();
}