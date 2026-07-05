using VideoCortex.Core.Entities;

namespace VideoCortex.Core.Services.Transcripts;

/// <summary>Per-video transcript processing: apply an Apify result and drive status/backoff/park.</summary>
public interface ITranscriptIngestRunner
{
    /// <summary>
    /// Process a single <c>Added</c>, un-parked video row. Always returns a result; never throws
    /// across this boundary except for a caller-driven <see cref="OperationCanceledException"/>.
    /// </summary>
    Task<TranscriptIngestResult> RunAsync(Video video, CancellationToken ct = default);
}

public enum TranscriptIngestOutcome
{
    Transcribed,
    NoTranscript,
    Retry,
    Parked,
}

public sealed record TranscriptIngestResult(
    TranscriptIngestOutcome Outcome,
    int RetryCount,
    string? Error,
    int ElapsedMs);
