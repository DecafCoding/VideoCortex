using System.Text.RegularExpressions;

namespace VideoCortex.Core.Services.Triage;

/// <summary>
/// Pure, DI-free parser that normalizes common YouTube URL shapes — or a bare 11-character
/// ID — down to a canonical video ID. Returns <see cref="YouTubeInputResult.Invalid"/> when
/// nothing recognizable can be extracted.
/// </summary>
/// <remarks>
/// Over-long/trailing-junk behavior (matches SkipWatch): the <c>{11}</c> capture groups take
/// the first 11 valid characters, so <c>watch?v=dQw4w9WgXcQextra</c> normalizes to
/// <c>dQw4w9WgXcQ</c>. A <b>bare</b> over-long token (e.g. 18 chars) fails the anchored
/// <c>^...{11}$</c> and is Invalid.
/// </remarks>
public static partial class YouTubeInputParser
{
    [GeneratedRegex("^[A-Za-z0-9_-]{11}$")]
    private static partial Regex BareId();

    [GeneratedRegex(@"[?&]v=([A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase)]
    private static partial Regex WatchV();

    [GeneratedRegex(@"youtu\.be/([A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase)]
    private static partial Regex ShortUrl();

    [GeneratedRegex(@"youtube\.com/shorts/([A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase)]
    private static partial Regex ShortsPath();

    [GeneratedRegex(@"youtube\.com/embed/([A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase)]
    private static partial Regex EmbedPath();

    [GeneratedRegex(@"youtube\.com/live/([A-Za-z0-9_-]{11})", RegexOptions.IgnoreCase)]
    private static partial Regex LivePath();

    public static YouTubeInputResult Parse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return YouTubeInputResult.Invalid;
        input = input.Trim();

        if (BareId().IsMatch(input)) return YouTubeInputResult.Valid(input);

        foreach (var re in new[] { WatchV(), ShortUrl(), ShortsPath(), EmbedPath(), LivePath() })
        {
            var m = re.Match(input);
            if (m.Success) return YouTubeInputResult.Valid(m.Groups[1].Value);
        }

        return YouTubeInputResult.Invalid;
    }
}
