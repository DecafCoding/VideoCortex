using VideoCortex.Features.Projects.Models;

namespace VideoCortex.Features.Projects.Services;

/// <summary>Create / list / fetch / delete projects, scaffolding the OKF library on create.</summary>
public interface IProjectService
{
    Task<SaveProjectResult> CreateAsync(ProjectFormModel model, CancellationToken ct = default);

    Task<IReadOnlyList<ProjectSummaryDto>> ListAsync(CancellationToken ct = default);

    Task<ProjectDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default);

    /// <summary>
    /// Deletes the project's DB rows (its videos cascade). The on-disk OKF library survives
    /// unless <paramref name="deleteLibraryFolder"/> is explicitly true — the folder is the
    /// precious durable artifact. When opted-in, only the project's own folder is removed.
    /// </summary>
    Task<DeleteProjectResult> DeleteAsync(int projectId, bool deleteLibraryFolder, CancellationToken ct = default);
}
