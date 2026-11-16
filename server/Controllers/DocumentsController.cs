using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StudyPilot.Api.Data;
using StudyPilot.Api.Dtos;
using StudyPilot.Api.Extensions;
using StudyPilot.Api.Models;
using StudyPilot.Api.Services;

namespace StudyPilot.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class DocumentsController : ControllerBase
{
  private const long MaxFileSizeBytes = 10 * 1024 * 1024;
  private const long MaxRequestSizeBytes = MaxFileSizeBytes + 1024 * 1024;
  private const int MaxTextLength = 200_000;
  private const int MaxTitleLength = 200;
  private const int MaxFileNameLength = 260;

  private readonly AppDbContext _db;
  private readonly TextExtractionService _textExtractor;

  public DocumentsController(AppDbContext db, TextExtractionService textExtractor)
  {
    _db = db;
    _textExtractor = textExtractor;
  }

  [HttpGet]
  public async Task<ActionResult<List<DocumentSummaryDto>>> GetAll()
  {
    var userId = User.GetUserId();

    var documents = await _db.Documents
        .Where(d => d.UserId == userId)
        .OrderByDescending(d => d.UploadedAt)
        .Select(d => new DocumentSummaryDto(
            d.Id,
            d.Title,
            d.OriginalFileName,
            d.UploadedAt,
            d.ExtractedText.Length,
            d.Quizzes.Count))
        .ToListAsync();

    return Ok(documents);
  }

  [HttpGet("{id:int}")]
  public async Task<ActionResult<DocumentDetailsDto>> GetById(int id)
  {
    var userId = User.GetUserId();

    var document = await _db.Documents
        .SingleOrDefaultAsync(d => d.Id == id && d.UserId == userId);

    if (document is null)
    {
      return NotFound();
    }

    return Ok(ToDetailsDto(document));
  }

  [HttpPost]
  [RequestSizeLimit(MaxRequestSizeBytes)]
  public async Task<ActionResult<DocumentDetailsDto>> Upload(
      [FromForm] UploadDocumentRequest request,
      CancellationToken cancellationToken)
  {
    var file = request.File;

    if (file.Length == 0)
    {
      return BadRequest(new { message = "The file is empty." });
    }

    if (file.Length > MaxFileSizeBytes)
    {
      return BadRequest(new { message = "The file is too large. The maximum size is 10 MB." });
    }

    string text;

    try
    {
      text = await _textExtractor.ExtractAsync(file, cancellationToken);
    }
    catch (TextExtractionException ex)
    {
      return BadRequest(new { message = ex.Message });
    }

    if (string.IsNullOrWhiteSpace(text))
    {
      return BadRequest(new
      {
        message = "No readable text was found in the file. Scanned documents (images of text) are not supported."
      });
    }

    if (text.Length > MaxTextLength)
    {
      return BadRequest(new
      {
        message = $"The document is too long. The maximum is {MaxTextLength:N0} characters."
      });
    }

    var fileName = Truncate(Path.GetFileName(file.FileName), MaxFileNameLength);
    var title = string.IsNullOrWhiteSpace(request.Title)
        ? Path.GetFileNameWithoutExtension(fileName)
        : request.Title.Trim();

    var document = new Document
    {
      UserId = User.GetUserId(),
      Title = Truncate(title, MaxTitleLength),
      OriginalFileName = fileName,
      ExtractedText = text
    };

    _db.Documents.Add(document);
    await _db.SaveChangesAsync(cancellationToken);

    return CreatedAtAction(nameof(GetById), new { id = document.Id }, ToDetailsDto(document));
  }

  [HttpDelete("{id:int}")]
  public async Task<IActionResult> Delete(int id)
  {
    var userId = User.GetUserId();

    var document = await _db.Documents
        .SingleOrDefaultAsync(d => d.Id == id && d.UserId == userId);

    if (document is null)
    {
      return NotFound();
    }

    _db.Documents.Remove(document);
    await _db.SaveChangesAsync();

    return NoContent();
  }

  private static DocumentDetailsDto ToDetailsDto(Document document) =>
      new(document.Id, document.Title, document.OriginalFileName, document.UploadedAt, document.ExtractedText);

  private static string Truncate(string value, int maxLength) =>
      value.Length <= maxLength ? value : value[..maxLength];
}