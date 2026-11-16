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

    var fixedPages = HebrewPdfTextFixer.Fix(pages);

    return string.Join("\n\n", fixedPages.Select(lines => string.Join('\n', lines)));
  }

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
}