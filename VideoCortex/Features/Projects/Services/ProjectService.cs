using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Library;
using VideoCortex.Core.Services.Utilities;
using VideoCortex.Features.Projects.Models;

namespace VideoCortex.Features.Projects.Services;

/// <summary>
/// Project CRUD over <see cref="VideoCortexDbContext"/>. On create, persists the row first,
/// then scaffolds the on-disk OKF library best-effort (a disk hiccup leaves a repairable
/// row-without-folder, never an orphan folder). Delete is rows-only unless the caller opts
/// into folder removal.
/// </summary>
public sealed class ProjectService(
    VideoCortexDbContext db,
    IOkfLibraryStore library,
    IOptions<LibrarySettings> libraryOptions,
    ILogger<ProjectService> logger) : IProjectService
{
    public async Task<SaveProjectResult> CreateAsync(ProjectFormModel model, CancellationToken ct = default)
    {
        var name = model.Name?.Trim();
        if (string.IsNullOrWhiteSpace(name))
            return new SaveProjectResult(false, null, "Name is required.", false);

        var description = Normalize(model.Description);
        var instructions = Normalize(model.AIInstructions);

        if (await db.Projects.AnyAsync(p => p.Name == name, ct))
            return new SaveProjectResult(false, null, $"A project named \"{name}\" already exists.", true);

        var baseSlug = SlugHelper.ToSlug(name, "project");
        var slug = await SlugHelper.UniqueSlugAsync(
            baseSlug, s => db.Projects.AnyAsync(p => p.Slug == s, ct), "project");

        var project = new Project
        {
            Name = name,
            Slug = slug,
            Description = description,
            AIInstructions = instructions,
            Status = ProjectStatus.Idle,
            CreatedAt = DateTime.UtcNow,
        };

        db.Projects.Add(project);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Lost a race on the unique Name/Slug index.
            logger.LogWarning(ex, "Create race for project {Name}", name);
            return new SaveProjectResult(false, null, $"A project named \"{name}\" already exists.", true);
        }

        // Scaffold the library on disk. Best-effort: the row is the source of truth.
        try
        {
            await library.CreateLibraryAsync(project, ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scaffold library for project {Slug}", project.Slug);
        }

        var dto = new ProjectSummaryDto(project.Id, project.Name, project.Slug, project.Description, 0, null);
        return new SaveProjectResult(true, dto, null, false);
    }

    public async Task<IReadOnlyList<ProjectSummaryDto>> ListAsync(CancellationToken ct = default)
        => await db.Projects.AsNoTracking()
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new ProjectSummaryDto(
                p.Id, p.Name, p.Slug, p.Description, p.Videos.Count, p.ReportUpdatedAt))
            .ToListAsync(ct);

    public async Task<ProjectDetailDto?> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var p = await db.Projects.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug, ct);
        if (p is null) return null;

        return new ProjectDetailDto(
            p.Id, p.Name, p.Slug, p.Description, p.AIInstructions, p.Status,
            p.ReportUpdatedAt, p.CreatedAt, OkfLibraryStore.SanitizeFolderName(p.Name),
            p.LastReportError);
    }

    public async Task<DeleteProjectResult> DeleteAsync(
        int projectId, bool deleteLibraryFolder, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
            return new DeleteProjectResult(false, "Project not found.");

        var folderName = OkfLibraryStore.SanitizeFolderName(project.Name);

        db.Projects.Remove(project); // Videos cascade (configured in Phase 1)
        await db.SaveChangesAsync(ct);

        if (deleteLibraryFolder)
        {
            // Only ever the project's own folder — never enumerate or touch siblings.
            try
            {
                var dir = Path.Combine(libraryOptions.Value.RootPath, folderName);
                if (Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to delete library folder for project {Slug}", project.Slug);
            }
        }

        return new DeleteProjectResult(true, null);
    }

    public async Task<bool> RequestReportRebuildAsync(int projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null) return false;

        project.ReportDirtySince = DateTime.UtcNow;
        project.ReportNextAttemptAt = null;
        project.ReportRetryCount = 0;
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static string? Normalize(string? s)
    {
        s = s?.Trim();
        return string.IsNullOrEmpty(s) ? null : s;
    }
}
