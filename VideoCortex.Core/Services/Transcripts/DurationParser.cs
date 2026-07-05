using System.Xml;

namespace VideoCortex.Core.Services.Transcripts;

/// <summary>Parses a video duration to whole seconds. Returns 0 for anything unrecognized.</summary>
public static class DurationParser
{
    /// <summary>
    /// Accepts ISO-8601 (<c>PT4M13S</c> → 253), a bare integer of seconds (<c>"253"</c> → 253),
    /// or <c>hh:mm:ss</c> / <c>mm:ss</c> (<c>"4:13"</c> → 253). Returns 0 (the "unknown"
    /// sentinel) on null/empty/malformed input so callers leave an existing value untouched.
    /// </summary>
    public static int ParseToSeconds(string? duration)
    {
        if (string.IsNullOrWhiteSpace(duration)) return 0;
        duration = duration.Trim();

        try
        {
            if (duration.StartsWith("PT", StringComparison.OrdinalIgnoreCase))
                return (int)XmlConvert.ToTimeSpan(duration).TotalSeconds;

            if (int.TryParse(duration, out var seconds))
                return seconds < 0 ? 0 : seconds;

            if (duration.Contains(':'))
            {
                var parts = duration.Split(':');
                var total = 0;
                foreach (var part in parts)
                {
                    if (!int.TryParse(part, out var n)) return 0;
                    total = total * 60 + n;
                }
                return total < 0 ? 0 : total;
            }
        }
        catch
        {
            return 0;
        }

        return 0;
    }
}
