using System.Text;
using System.Text.RegularExpressions;

namespace VideoCortex.Core.Services.Transcripts;

/// <summary>Converts SRT subtitles to the transcript line format <c>[mm:ss] text</c>.</summary>
public static partial class SrtConverter
{
    [GeneratedRegex(@"(\d+):(\d+):(\d+),\d+")]
    private static partial Regex TimingLine();

    /// <summary>
    /// SRT → <c>[mm:ss] text</c> lines. The hours field is folded into minutes (a 65-minute
    /// video renders <c>[65:03]</c>), matching the format the summarizer expects.
    /// </summary>
    public static string ConvertSrtToPrdFormat(string srtContent)
    {
        if (string.IsNullOrWhiteSpace(srtContent)) return string.Empty;

        var output = new StringBuilder();
        var entries = srtContent.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var entry in entries)
        {
            var lines = entry.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2) continue;

            var match = TimingLine().Match(lines[1]);
            if (!match.Success) continue;

            var hours = int.Parse(match.Groups[1].Value);
            var minutes = int.Parse(match.Groups[2].Value);
            var seconds = int.Parse(match.Groups[3].Value);
            var totalMinutes = hours * 60 + minutes;
            var formattedTime = $"{totalMinutes:D2}:{seconds:D2}";

            var textBuilder = new StringBuilder();
            for (int i = 2; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;
                textBuilder.Append(lines[i].Trim() + " ");
            }

            var subtitleText = textBuilder.ToString().Trim();
            if (string.IsNullOrWhiteSpace(subtitleText)) continue;

            output.AppendLine($"[{formattedTime}] {subtitleText}");
        }

        return output.ToString();
    }
}
