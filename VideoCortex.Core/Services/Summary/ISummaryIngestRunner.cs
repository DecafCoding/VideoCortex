using VideoCortex.Core.Entities;

namespace VideoCortex.Core.Services.Summary;

/// <summary>Per-video summarization: summarize → write concept page → advance to Summarized.</summary>
public interface ISummaryIngestRunner
{
    /// <summary>
    /// Process one <c>Transcribed</c>, un-parked video. Always returns a result; never throws
    /// across this boundary except a caller-driven <see cref="OperationCanceledException"/> and an
    /// <see cref="InvalidOperationException"/> for a precondition breach (empty transcript).
    /// </summary>
    Task<SummaryIngestResult> RunAsync(Video video, CancellationToken ct = default);
}

public enum SummaryIngestOutcome
{
    Summarized,
    Retry,
    Parked,
}

public sealed record SummaryIngestResult(
    SummaryIngestOutcome Outcome,
    int RetryCount,
    string? Error,
    int ElapsedMs);
