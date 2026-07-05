namespace VideoCortex.Core.Services.Triage;

/// <summary>
/// Outcome of parsing user input into a canonical YouTube video ID. Distinguishes a valid
/// parse (with <see cref="VideoId"/>) from unrecognizable input.
/// </summary>
public record YouTubeInputResult(bool IsValid, string? VideoId)
{
    public static YouTubeInputResult Valid(string videoId) => new(true, videoId);

    public static YouTubeInputResult Invalid { get; } = new(false, null);
}
