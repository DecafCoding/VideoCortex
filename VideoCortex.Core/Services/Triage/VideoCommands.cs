using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;

namespace VideoCortex.Core.Services.Triage;

/// <summary>
/// Adds videos to a project by URL/ID. Duplicate detection is index-backed: the pre-insert
/// check is a courtesy; the unique <c>(ProjectId, YoutubeVideoId)</c> index is the real guard,
/// and the <see cref="DbUpdateException"/> catch closes the check-then-insert race.
/// </summary>
public sealed class VideoCommands(VideoCortexDbContext db, ILogger<VideoCommands> logger) : IVideoCommands
{
    public async Task<AddVideoResult> AddVideoByUrlAsync(int projectId, string input, CancellationToken ct = default)
    {
        var parsed = YouTubeInputParser.Parse(input);
        if (!parsed.IsValid)
            return AddVideoResult.Invalid("Couldn't recognize that as a YouTube video URL or ID.");

        var videoId = parsed.VideoId!;

        if (!await db.Projects.AnyAsync(p => p.Id == projectId, ct))
            return AddVideoResult.Invalid($"Project {projectId} not found.");

        if (await db.Videos.AnyAsync(v => v.ProjectId == projectId && v.YoutubeVideoId == videoId, ct))
            return AddVideoResult.Duplicate(videoId);

        var video = new Video
        {
            ProjectId = projectId,
            YoutubeVideoId = videoId,
            Status = VideoStatus.Added,
            AddedAt = DateTime.UtcNow,
        };
        db.Videos.Add(video);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            // Lost the check-then-insert race on the unique (ProjectId, YoutubeVideoId) index.
            logger.LogWarning(ex, "Add-video race for {VideoId} in project {ProjectId}", videoId, projectId);
            db.Entry(video).State = EntityState.Detached;
            return AddVideoResult.Duplicate(videoId);
        }

        return AddVideoResult.Added(video.Id, video.YoutubeVideoId);
    }
}
