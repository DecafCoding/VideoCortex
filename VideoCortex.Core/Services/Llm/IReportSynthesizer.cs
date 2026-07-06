namespace VideoCortex.Core.Services.Llm;

/// <summary>
/// Synthesizes a project's cross-video report from the compact per-video summaries (never from
/// transcripts). Returns inner-body HTML that the library store wraps in the OKF root-index shell.
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

/// <summary>The synthesizer's structured output.</summary>
public sealed record ReportSynthesisResult(string LibraryDescription, string ReportHtml);

/// <summary>Thrown when the synthesizer's structured output is unparseable or missing content.</summary>
public sealed class ReportSynthesisException : Exception
{
    public ReportSynthesisException(string message, Exception? inner = null) : base(message, inner) { }
}
