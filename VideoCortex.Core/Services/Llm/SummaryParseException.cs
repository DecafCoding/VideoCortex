namespace VideoCortex.Core.Services.Llm;

/// <summary>
/// Thrown when the LLM's structured-output response fails to deserialize or is missing required
/// content. The ingest runner treats it as a transient failure (retry/park).
/// </summary>
public sealed class SummaryParseException : Exception
{
    public SummaryParseException(string message, Exception? inner = null) : base(message, inner) { }
}
