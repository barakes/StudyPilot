namespace StudyPilot.Api.Services;

public class TextExtractionException : Exception
{
  public TextExtractionException(string message, Exception? innerException = null)
      : base(message, innerException)
  {
  }
}