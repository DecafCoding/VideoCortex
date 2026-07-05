using Microsoft.EntityFrameworkCore;
using VideoCortex.Core.Db;
using VideoCortex.Core.Services.Triage.Dtos;

namespace VideoCortex.Core.Services.Triage;

/// <summary>Projection-to-DTO reads over the DbContext (no entity tracking).</summary>
public sealed class VideoQueries(VideoCortexDbContext db) : IVideoQueries
{
    public async Task<IReadOnlyList<VideoRowDto>> ListForProjectAsync(int projectId, CancellationToken ct = default)
        => await db.Videos.AsNoTracking()
            .Where(v => v.ProjectId == projectId)
            .OrderByDescending(v => v.AddedAt)
            .Select(v => new VideoRowDto(v.Id, v.YoutubeVideoId, v.Title, v.Status, v.AddedAt))
            .ToListAsync(ct);
}
