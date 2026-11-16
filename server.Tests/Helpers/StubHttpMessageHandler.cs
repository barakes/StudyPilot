using System.Net;
using System.Text;
using System.Text.Json;

namespace StudyPilot.Api.Tests.Helpers;

// Stands in for the network in tests: records every request and returns a fixed response.
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
  private readonly HttpStatusCode _statusCode;
  private readonly string _responseBody;

  public StubHttpMessageHandler(HttpStatusCode statusCode, string responseBody)
  {
    _statusCode = statusCode;
    _responseBody = responseBody;
  }

  public List<string> RequestBodies { get; } = new();

  protected override async Task<HttpResponseMessage> SendAsync(
      HttpRequestMessage request,
      CancellationToken cancellationToken)
  {
    RequestBodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

    return new HttpResponseMessage(_statusCode)
    {
      Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
    };
  }

  // Wraps a JSON answer in the envelope that the Gemini API returns.
  public static string GeminiResponse(string innerJson) =>
      JsonSerializer.Serialize(new
      {
        candidates = new[]
          {
                new { content = new { parts = new[] { new { text = innerJson } } } }
          }
      });

  // Reads the user prompt text out of a recorded Gemini request.
  public static string GetUserPrompt(string requestBody)
  {
    using var document = JsonDocument.Parse(requestBody);

    return document.RootElement
        .GetProperty("contents")[0]
        .GetProperty("parts")[0]
        .GetProperty("text")
        .GetString()!;
  }
}