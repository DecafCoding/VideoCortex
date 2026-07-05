namespace VideoCortex.Core.Services.Report;

/// <summary>Regenerates exactly one project's report (root index.html) per call.</summary>
public interface IReportRegenerationRunner
{
    Task<ReportRegenerationResult> RunForProjectAsync(int projectId, CancellationToken ct = default);
}

public enum ReportRegenerationOutcome
{
    NoOp,
    Regenerated,
    Retry,
    Parked,
}

public sealed record ReportRegenerationResult(
    ReportRegenerationOutcome Outcome,
    int VideosFolded,
    string? Error);
