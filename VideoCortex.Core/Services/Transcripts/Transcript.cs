namespace VideoCortex.Core.Services.Transcripts;

/// <summary>
/// Payload returned by <see cref="ITranscriptSource.FetchAsync"/>. Carries the transcript plus
/// the metadata Apify returns in the same call (title, channel, description, duration, counts,
/// thumbnail) — there is no YouTube Data API, so Apify is the sole source of these fields.
/// </summary>
/// <remarks>
/// Field order is a contract: the runner and every test construct this positionally. Do not reorder.
/// </remarks>
public sealed record Transcript(
    bool Success,
    string? TranscriptText,
    string? TranscriptLang,
    bool HasTranscript,
    string? Title,
    string? ChannelTitle,
    string? Description,
    int? DurationSeconds,
    long? ViewCount,
    long? LikeCount,
    long? CommentsCount,
    string? ThumbnailUrl,
    string? ErrorMessage)
{
    /// <summary>Builds a failed result carrying only the error message.</summary>
    public static Transcript Failure(string message) =>
        new(false, null, null, false, null, null, null, null, null, null, null, null, message);
}
