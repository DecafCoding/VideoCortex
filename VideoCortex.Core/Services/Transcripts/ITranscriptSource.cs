namespace VideoCortex.Core.Services.Transcripts;

/// <summary>
/// Single seam over the transcript provider. The MVP implementation
/// (<see cref="ApifyTranscriptSource"/>) hits the Apify <c>streamers/youtube-scraper</c> actor;
/// keeping the surface to one method leaves room for a local Whisper/yt-dlp provider later.
/// </summary>
public interface ITranscriptSource
{
    Task<Transcript> FetchAsync(string videoId, CancellationToken ct = default);
}
