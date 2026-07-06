namespace VideoCortex.Core.Services.Llm;

/// <summary>
/// Aggregates a project's cross-video report from the compact per-video summaries (never from
/// transcripts). The report is a <b>cumulative, deduplicated index of atomic items</b> (what an
/// "item" is is defined by the project's AI instructions): every distinct item from every video
/// appears, items that recur across videos are merged into one entry that cites all its sources.
/// Returns structured data (a list of <see cref="ReportItem"/>); the library store renders the
/// running-list HTML deterministically.
/// </summary>
public interface IReportSynthesizer
{
    Task<ReportSynthesisResult> SynthesizeAsync(ReportSynthesisContext ctx, CancellationToken ct = default);
}

/// <summary>One video's compact summary, as fed to the report synthesizer.</summary>
public sealed record VideoSummaryInput(
    int VideoId,
    string? YoutubeVideoId,
    string Title,
    string? ChannelTitle,
    string ConceptSlug,
    string? SummaryTitle,
    string? SummaryDescription,
    string? SummaryBodyMd);

/// <summary>Everything the synthesizer needs to write a report for one project.</summary>
public sealed record ReportSynthesisContext(
    string ProjectName,
    string? AIInstructions,
    IReadOnlyList<VideoSummaryInput> Videos);

/// <summary>The synthesizer's structured output: a one-line library description plus the merged item list.</summary>
public sealed record ReportSynthesisResult(string LibraryDescription, IReadOnlyList<ReportItem> Items);

/// <summary>
/// One entry in the report's running list. <paramref name="SourceSlugs"/> holds the concept slug of
/// every video the item was drawn from (more than one when the item was merged across videos); the
/// library store renders these as the per-item "Sources" line.
/// </summary>
public sealed record ReportItem(string Title, string BodyMarkdown, IReadOnlyList<string> SourceSlugs);

/// <summary>Thrown when the synthesizer's structured output is unparseable or missing content.</summary>
public sealed class ReportSynthesisException : Exception
{
    public ReportSynthesisException(string message, Exception? inner = null) : base(message, inner) { }
}
