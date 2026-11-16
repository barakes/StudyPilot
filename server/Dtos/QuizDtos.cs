using System.ComponentModel.DataAnnotations;

namespace StudyPilot.Api.Dtos;

public class GenerateQuizRequest
{
  [Range(1, int.MaxValue)]
  public int DocumentId { get; set; }

  [Range(0, 15)]
  public int MultipleChoiceCount { get; set; } = 5;

  [Range(0, 10)]
  public int OpenCount { get; set; } = 2;
}

public record QuizSummaryDto(
    int Id,
    string Title,
    DateTime CreatedAt,
    int DocumentId,
    string DocumentTitle,
    int QuestionCount,
    int AttemptCount,
    double? BestScore);

public record QuizDetailsDto(
    int Id,
    string Title,
    DateTime CreatedAt,
    int DocumentId,
    string DocumentTitle,
    List<QuestionDto> Questions);

public record QuestionDto(
    int Id,
    int Order,
    string Type,
    string Text,
    List<string> Options);