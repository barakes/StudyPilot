using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace StudyPilot.Api.Services;

public partial class TextExtractionService
{
  public static readonly string[] SupportedExtensions = [".txt", ".docx", ".pdf"];

  private static readonly HashSet<char> HebrewFinalLetters = ['ך', 'ם', 'ן', 'ף', 'ץ'];

  public async Task<string> ExtractAsync(IFormFile file, CancellationToken cancellationToken = default)
  {
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

    if (!SupportedExtensions.Contains(extension))
    {
      throw new TextExtractionException(
          $"Unsupported file type '{extension}'. Supported types: {string.Join(", ", SupportedExtensions)}.");
    }

    await using var buffer = new MemoryStream();
    await file.CopyToAsync(buffer, cancellationToken);
    buffer.Position = 0;

    string text;

    try
    {
      text = extension switch
      {
        ".txt" => await ExtractFromTxtAsync(buffer, cancellationToken),
        ".docx" => ExtractFromDocx(buffer),
        ".pdf" => ExtractFromPdf(buffer),
        _ => throw new TextExtractionException($"Unsupported file type '{extension}'.")
      };
    }
    catch (TextExtractionException)
    {
      throw;
    }
    catch (Exception ex)
    {
      throw new TextExtractionException(
          "The file could not be read. It may be corrupted or password-protected.", ex);
    }

    return Normalize(text);
  }

  private static async Task<string> ExtractFromTxtAsync(Stream stream, CancellationToken cancellationToken)
  {
    using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
    return await reader.ReadToEndAsync(cancellationToken);
  }

  private static string ExtractFromDocx(Stream stream)
  {
    using var document = WordprocessingDocument.Open(stream, false);
    var body = document.MainDocumentPart?.Document?.Body;

    if (body is null)
    {
      return string.Empty;
    }

    var paragraphs = body.Descendants<Paragraph>().Select(p => p.InnerText);
    return string.Join('\n', paragraphs);
  }

  private static string ExtractFromPdf(Stream stream)
  {
    using var pdf = PdfDocument.Open(stream);

    var pages = pdf.GetPages()
        .Select(page => ContentOrderTextExtractor.GetText(page)
            .Replace("\r\n", "\n")
            .Split('\n')
            .ToList())
        .ToList();

    var (reversedWords, normalWords) = pages
        .SelectMany(lines => lines)
        .Select(ScoreFinalLetters)
        .Aggregate((0, 0), (total, score) => (total.Item1 + score.Reversed, total.Item2 + score.Normal));

    var documentLooksReversed = reversedWords > normalWords;

    pages = pages
        .Select(lines => lines.Select(line => FixHebrewLine(line, documentLooksReversed)).ToList())
        .ToList();

    pages = RemoveRepeatedLines(pages);

    return string.Join("\n\n", pages.Select(lines => string.Join('\n', lines)));
  }

  // Some PDFs store Hebrew in visual (left-to-right) order, so words come out reversed.
  // Hebrew final letters only appear at the end of a word, which lets us detect reversed lines.
  private static string FixHebrewLine(string line, bool documentLooksReversed)
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

  private static string Normalize(string text)
  {
    var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
    normalized = MultipleSpacesRegex().Replace(normalized, " ");
    normalized = string.Join('\n', normalized.Split('\n').Select(line => line.Trim()));
    normalized = ExcessBlankLinesRegex().Replace(normalized, "\n\n");
    return normalized.Trim();
  }

  [GeneratedRegex(@"[ \t]+")]
  private static partial Regex MultipleSpacesRegex();

  [GeneratedRegex(@"\n{3,}")]
  private static partial Regex ExcessBlankLinesRegex();

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