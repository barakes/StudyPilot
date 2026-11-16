using System.ComponentModel.DataAnnotations;

namespace StudyPilot.Api.Dtos;

public class UploadDocumentRequest
{
  [Required]
  public IFormFile File { get; set; } = null!;

  [MaxLength(200)]
  public string? Title { get; set; }
}

public record DocumentSummaryDto(
    int Id,
    string Title,
    string OriginalFileName,
    DateTime UploadedAt,
    int CharacterCount,
    int QuizCount);

public record DocumentDetailsDto(
    int Id,
    string Title,
    string OriginalFileName,
    DateTime UploadedAt,
    string ExtractedText);