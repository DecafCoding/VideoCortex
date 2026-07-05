using VideoCortex.Core.Entities;

namespace VideoCortex.Features.Projects.Models;

/// <summary>A project row for the list page.</summary>
public record ProjectSummaryDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    int VideoCount,
    DateTime? ReportUpdatedAt);

/// <summary>Full project detail for <c>/projects/{slug}</c>.</summary>
public record ProjectDetailDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    string? AIInstructions,
    ProjectStatus Status,
    DateTime? ReportUpdatedAt,
    DateTime CreatedAt,
    string LibraryFolderName);

/// <summary>Outcome of a create/save attempt.</summary>
public record SaveProjectResult(
    bool Success,
    ProjectSummaryDto? Project,
    string? ErrorMessage,
    bool IsDuplicate);

/// <summary>Outcome of a delete attempt.</summary>
public record DeleteProjectResult(bool Success, string? ErrorMessage);
