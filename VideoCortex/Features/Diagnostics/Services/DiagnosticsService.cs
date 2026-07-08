using Microsoft.EntityFrameworkCore;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;
using VideoCortex.Features.Diagnostics.Models;

namespace VideoCortex.Features.Diagnostics.Services;

/// <summary>Projection-to-DTO reads over the DbContext for the Diagnostics page (no tracking).</summary>
public sealed class DiagnosticsService(VideoCortexDbContext db) : IDiagnosticsService
{
    public async Task<IReadOnlyList<ProjectIssueDto>> GetProjectIssuesAsync(CancellationToken ct = default)
        => await db.Projects.AsNoTracking()
            .Where(p => p.Status == ProjectStatus.Error || p.LastReportError != null)
            .OrderBy(p => p.Name)
            .Select(p => new ProjectIssueDto(
                p.Id, p.Name, p.Slug, p.Status, p.ReportRetryCount,
                p.ReportNextAttemptAt, p.ReportDirtySince, p.ReportUpdatedAt, p.LastReportError))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<VideoIssueDto>> GetVideoIssuesAsync(CancellationToken ct = default)
        => await db.Videos.AsNoTracking()
            .Where(v => v.Parked
                || v.LastError != null
                || v.Status == VideoStatus.Error
                || v.Status == VideoStatus.NoTranscript)
            .OrderByDescending(v => v.AddedAt)
            .Select(v => new VideoIssueDto(
                v.Id, v.Project!.Name, v.Project!.Slug, v.YoutubeVideoId, v.Title,
                v.Status, v.Parked, v.RetryCount, v.NextAttemptAt, v.LastError, v.AddedAt))
            .ToListAsync(ct);
}
