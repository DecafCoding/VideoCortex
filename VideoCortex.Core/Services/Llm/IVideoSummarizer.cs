namespace VideoCortex.Core.Services.Llm;

/// <summary>Produces a structured, reasonably complete summary of one video from its transcript.</summary>
public interface IVideoSummarizer
{
    Task<VideoSummary> SummarizeAsync(
        string title,
        string channel,
        string transcript,
        string? aiInstructions = null,
        CancellationToken ct = default);
}
