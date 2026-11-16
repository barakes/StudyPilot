using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StudyPilot.Api.Models;

namespace StudyPilot.Api.Services.Ai;

public class GeminiSettings
{
  public const string SectionName = "Gemini";

  public string ApiKey { get; set; } = string.Empty;
  public string Model { get; set; } = "gemini-3.5-flash-lite";
}

public class GeminiQuestionGenerator : IQuestionGenerator
{
  // Keeps each request well inside the free tier's tokens-per-minute limit.
  private const int MaxSourceCharacters = 100_000;

  private const string SystemPrompt = """
        You are an experienced teacher who writes high-quality practice quizzes for students.

        Rules:
        - Base every question only on the study material provided. Do not use outside knowledge.
        - Write the title, questions, options, answers and explanations in the same language as the study material.
        - Focus on the important concepts. Skip course logistics such as dates, lecturer names, page numbers and exercise instructions.
        - The material was extracted automatically from a file and may contain broken lines or misplaced words. Infer the intended meaning and ignore extraction artifacts.
        - Multiple choice questions: exactly 4 options and exactly one correct option. Wrong options must be plausible. Do not use "all of the above" or "none of the above".
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

  private readonly HttpClient _httpClient;
  private readonly GeminiSettings _settings;
  private readonly ILogger<GeminiQuestionGenerator> _logger;

  public GeminiQuestionGenerator(
      HttpClient httpClient,
      IOptions<GeminiSettings> options,
      ILogger<GeminiQuestionGenerator> logger)
  {
    _httpClient = httpClient;
    _settings = options.Value;
    _logger = logger;
  }

  public async Task<GeneratedQuiz> GenerateAsync(
      string sourceText,
      QuizGenerationOptions options,
      CancellationToken cancellationToken = default)
  {
    var material = sourceText.Length > MaxSourceCharacters ? sourceText[..MaxSourceCharacters] : sourceText;

    var requestBody = new
    {
      systemInstruction = new { parts = new[] { new { text = SystemPrompt } } },
      contents = new[]
        {
                new { role = "user", parts = new[] { new { text = BuildUserPrompt(material, options) } } }
            },
      generationConfig = new
      {
        responseMimeType = "application/json",
        responseJsonSchema = ResponseSchema,
        temperature = 0.4
      }
    };

    using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{_settings.Model}:generateContent")
    {
      Content = JsonContent.Create(requestBody, options: JsonOptions)
    };
    request.Headers.Add("x-goog-api-key", _settings.ApiKey);

    HttpResponseMessage response;

    try
    {
      response = await _httpClient.SendAsync(request, cancellationToken);
    }
    catch (HttpRequestException ex)
    {
      throw new QuestionGenerationException("Could not reach the AI service. Please try again.", ex);
    }
    catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
    {
      throw new QuestionGenerationException("The AI service took too long to respond. Please try again.", ex);
    }

    using (response)
    {
      var body = await response.Content.ReadAsStringAsync(cancellationToken);

      if (response.StatusCode == HttpStatusCode.TooManyRequests)
      {
        throw new QuestionGenerationException("The free AI quota has been reached. Please try again later.");
      }

      if (!response.IsSuccessStatusCode)
      {
        _logger.LogError("Gemini returned status {StatusCode}: {Body}", (int)response.StatusCode, body);
        throw new QuestionGenerationException("The AI service returned an error. Please try again later.");
      }

      return ParseResponse(body, options);
    }
  }

  private static string BuildUserPrompt(string material, QuizGenerationOptions options) =>
      $"""
        Create a quiz with exactly {options.MultipleChoiceCount} multiple choice questions and exactly {options.OpenCount} open questions, based on the following study material.

        <material>
        {material}
        </material>
        """;

  private static GeneratedQuiz ParseResponse(string body, QuizGenerationOptions options)
  {
    string json;

    try
    {
      using var document = JsonDocument.Parse(body);

      if (!document.RootElement.TryGetProperty("candidates", out var candidates)
          || candidates.GetArrayLength() == 0
          || !candidates[0].TryGetProperty("content", out var content)
          || !content.TryGetProperty("parts", out var parts))
      {
        throw new QuestionGenerationException(
            "The AI service did not return any questions. The document may have been blocked by its safety filters.");
      }

      // Skip "thought" parts, which some models return before the actual answer.
      json = string.Concat(parts.EnumerateArray()
          .Where(part => !(part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
          .Select(part => part.TryGetProperty("text", out var text) ? text.GetString() : null));
    }
    catch (JsonException ex)
    {
      throw new QuestionGenerationException("The AI service returned an invalid response. Please try again.", ex);
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

  private sealed record GeminiQuiz(string? Title, List<GeminiQuestion>? Questions);

  private sealed record GeminiQuestion(
      string? Type,
      string? Question,
      List<string>? Options,
      string? CorrectAnswer,
      string? Explanation);
}