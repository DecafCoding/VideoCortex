using VideoCortex.Core.Entities;

namespace VideoCortex.Features.Diagnostics.Models;

/// <summary>A project with a failed (or previously failed) report regeneration.</summary>
public record ProjectIssueDto(
    int Id,
    string Name,
    string Slug,
    ProjectStatus Status,
    int ReportRetryCount,
    DateTime? ReportNextAttemptAt,
    DateTime? ReportDirtySince,
    DateTime? ReportUpdatedAt,
    string? LastReportError)
{
    /// <summary>Retries exhausted: the worker stopped selecting it until a manual Rebuild Report.</summary>
    public bool IsParked => Status == ProjectStatus.Error && ReportDirtySince is null;
}

/// <summary>A video whose pipeline stage failed, parked, or found no transcript.</summary>
public record VideoIssueDto(
    int Id,
    string ProjectName,
    string ProjectSlug,
    string YoutubeVideoId,
    string? Title,
    VideoStatus Status,
    bool Parked,
    int RetryCount,
    DateTime? NextAttemptAt,
    string? LastError,
    DateTime AddedAt);
