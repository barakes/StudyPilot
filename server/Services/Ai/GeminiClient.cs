using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace StudyPilot.Api.Services.Ai;

public class GeminiException : Exception
{
  public GeminiException(string message, Exception? innerException = null)
      : base(message, innerException)
  {
  }
}

// Low-level client for the Gemini API. It sends a prompt with a JSON schema and returns the JSON text.
// Both the question generator and the answer grader use it, so the HTTP and error handling live in one place.
public class GeminiClient
{
  private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

  private readonly HttpClient _httpClient;
  private readonly GeminiSettings _settings;
  private readonly ILogger<GeminiClient> _logger;

  public GeminiClient(HttpClient httpClient, IOptions<GeminiSettings> options, ILogger<GeminiClient> logger)
  {
    _httpClient = httpClient;
    _settings = options.Value;
    _logger = logger;
  }

  public async Task<string> GenerateJsonAsync(
      string systemPrompt,
      string userPrompt,
      object responseSchema,
      double temperature,
      CancellationToken cancellationToken = default)
  {
    var requestBody = new
    {
      systemInstruction = new { parts = new[] { new { text = systemPrompt } } },
      contents = new[]
        {
                new { role = "user", parts = new[] { new { text = userPrompt } } }
            },
      generationConfig = new
      {
        responseMimeType = "application/json",
        responseJsonSchema = responseSchema,
        temperature
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
      throw new GeminiException("Could not reach the AI service. Please try again.", ex);
    }
    catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
    {
      throw new GeminiException("The AI service took too long to respond. Please try again.", ex);
    }

    using (response)
    {
      var body = await response.Content.ReadAsStringAsync(cancellationToken);

      if (response.StatusCode == HttpStatusCode.TooManyRequests)
      {
        throw new GeminiException("The free AI quota has been reached. Please try again later.");
      }

      if (!response.IsSuccessStatusCode)
      {
        _logger.LogError("Gemini returned status {StatusCode}: {Body}", (int)response.StatusCode, body);
        throw new GeminiException("The AI service returned an error. Please try again later.");
      }

      return ExtractText(body);
    }
  }

  private static string ExtractText(string body)
  {
    try
    {
      using var document = JsonDocument.Parse(body);

      if (!document.RootElement.TryGetProperty("candidates", out var candidates)
          || candidates.GetArrayLength() == 0
          || !candidates[0].TryGetProperty("content", out var content)
          || !content.TryGetProperty("parts", out var parts))
      {
        throw new GeminiException(
            "The AI service did not return a result. The content may have been blocked by its safety filters.");
      }

      // Skip "thought" parts, which some models return before the actual answer.
      return string.Concat(parts.EnumerateArray()
          .Where(part => !(part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
          .Select(part => part.TryGetProperty("text", out var text) ? text.GetString() : null));
    }
    catch (JsonException ex)
    {
      throw new GeminiException("The AI service returned an invalid response. Please try again.", ex);
    }
  }
}