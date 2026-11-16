using System.Text;
using System.Text.Json;
using StudyPilot.Api.Models;

namespace StudyPilot.Api.Services.Ai;

public class GeminiQuestionGenerator : IQuestionGenerator
{
  // Keeps each request well inside the free tier's tokens-per-minute limit.
  private const int MaxSourceCharacters = 100_000;

  // A little higher than for grading, so repeated quizzes on the same material are more varied.
  private const double Temperature = 0.7;

  private const string SystemPrompt = """
        You are an experienced teacher who writes high-quality practice quizzes for students.

        Rules:
        - Base every question only on the study material provided. Do not use outside knowledge.
        - Write the title, questions, options, answers and explanations in the same language as the study material.
        - Focus on the important concepts. Skip course logistics such as dates, lecturer names, page numbers and exercise instructions.
        - The material was extracted automatically from a file and may contain broken lines or misplaced words. Infer the intended meaning and ignore extraction artifacts.
        - Multiple choice questions: exactly 4 options and exactly one correct option. Wrong options must be plausible. Do not use "all of the above" or "none of the above".
        - Every option must differ in meaning from all the other options. Never include two options that are logically equivalent, even if they are worded differently (for example, "first in, first out" and "last in, last out" describe the same behavior).
        - For multiple choice questions, correctAnswer must be copied exactly, character for character, from one of the options.
        - Open questions: options is an empty list, and correctAnswer is a short model answer (1 to 3 sentences) that a teacher would accept.
        - explanation: 1 to 2 sentences explaining why the answer is correct, based on the material.
        - title: a short title describing the topic of the quiz.
        """;

  private static readonly object ResponseSchema = new
  {
    type = "object",
    properties = new
    {
      title = new { type = "string" },
      questions = new
      {
        type = "array",
        items = new
        {
          type = "object",
          properties = new
          {
            type = new { type = "string", @enum = new[] { "multiple_choice", "open" } },
            question = new { type = "string" },
            options = new { type = "array", items = new { type = "string" } },
            correctAnswer = new { type = "string" },
            explanation = new { type = "string" }
          },
          required = new[] { "type", "question", "options", "correctAnswer", "explanation" }
        }
      }
    },
    required = new[] { "title", "questions" }
  };

  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  private readonly GeminiClient _gemini;

  public GeminiQuestionGenerator(GeminiClient gemini)
  {
    _gemini = gemini;
  }

  public async Task<GeneratedQuiz> GenerateAsync(
      string sourceText,
      QuizGenerationOptions options,
      CancellationToken cancellationToken = default)
  {
    var material = sourceText.Length > MaxSourceCharacters ? sourceText[..MaxSourceCharacters] : sourceText;

    string json;

    try
    {
      json = await _gemini.GenerateJsonAsync(
          SystemPrompt,
          BuildUserPrompt(material, options),
          ResponseSchema,
          Temperature,
          cancellationToken);
    }
    catch (GeminiException ex)
    {
      throw new QuestionGenerationException(ex.Message, ex);
    }

    GeminiQuiz? quiz;

    try
    {
      quiz = JsonSerializer.Deserialize<GeminiQuiz>(json, JsonOptions);
    }
    catch (JsonException ex)
    {
      throw new QuestionGenerationException("The AI service returned an invalid response. Please try again.", ex);
    }

    if (quiz?.Questions is null)
    {
      throw new QuestionGenerationException("The AI service returned an empty quiz. Please try again.");
    }

    var multipleChoice = quiz.Questions
        .Where(q => q.Type == "multiple_choice")
        .Take(options.MultipleChoiceCount)
        .Select(q => new GeneratedQuestion(
            QuestionType.MultipleChoice,
            q.Question ?? string.Empty,
            q.Options ?? new List<string>(),
            q.CorrectAnswer ?? string.Empty,
            q.Explanation ?? string.Empty));

    var open = quiz.Questions
        .Where(q => q.Type == "open")
        .Take(options.OpenCount)
        .Select(q => new GeneratedQuestion(
            QuestionType.Open,
            q.Question ?? string.Empty,
            new List<string>(),
            q.CorrectAnswer ?? string.Empty,
            q.Explanation ?? string.Empty));

    return new GeneratedQuiz(quiz.Title ?? string.Empty, multipleChoice.Concat(open).ToList());
  }

  private static string BuildUserPrompt(string material, QuizGenerationOptions options)
  {
    var builder = new StringBuilder();

    builder.AppendLine(
        $"Create a quiz with exactly {options.MultipleChoiceCount} multiple choice questions " +
        $"and exactly {options.OpenCount} open questions, based on the following study material.");
    builder.AppendLine();
    builder.AppendLine("<material>");
    builder.AppendLine(material);
    builder.AppendLine("</material>");

    if (options.QuestionsToAvoid is { Count: > 0 })
    {
      builder.AppendLine();
      builder.AppendLine(
          "The student has already practiced the questions below on this material. " +
          "Write new questions that test different facts, or the same concepts from a clearly different angle " +
          "(for example: an example instead of a definition, a comparison, or applying the idea to a new case). " +
          "Do not repeat or simply rephrase these questions.");
      builder.AppendLine("<previous_questions>");

      foreach (var question in options.QuestionsToAvoid)
      {
        builder.AppendLine($"- {question}");
      }

      builder.AppendLine("</previous_questions>");
    }

    return builder.ToString();
  }

  private sealed record GeminiQuiz(string? Title, List<GeminiQuestion>? Questions);

  private sealed record GeminiQuestion(
      string? Type,
      string? Question,
      List<string>? Options,
      string? CorrectAnswer,
      string? Explanation);
}