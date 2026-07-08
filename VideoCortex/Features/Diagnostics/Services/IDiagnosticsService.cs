using VideoCortex.Features.Diagnostics.Models;

namespace VideoCortex.Features.Diagnostics.Services;

/// <summary>Read-only queries backing the Diagnostics page.</summary>
public interface IDiagnosticsService
{
    /// <summary>Projects in Error status or carrying a recorded report failure.</summary>
    Task<IReadOnlyList<ProjectIssueDto>> GetProjectIssuesAsync(CancellationToken ct = default);

    /// <summary>Videos that are parked, in Error/NoTranscript, or carrying a recorded failure.</summary>
    Task<IReadOnlyList<VideoIssueDto>> GetVideoIssuesAsync(CancellationToken ct = default);
}
