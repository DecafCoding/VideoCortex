using Microsoft.EntityFrameworkCore;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;

namespace VideoCortex.Core.Services.Triage;

/// <summary>
/// Resets a parked/errored video so a worker repicks it. Clears the park fields and routes
/// <see cref="Video.Status"/> back to the pending stage inferred from which columns are populated
/// (not the raw status), so a failure at any stage recovers to the exact status its worker polls:
/// no transcript → <c>Added</c>; transcript but no summary → <c>Transcribed</c>; summarized →
/// <c>Summarized</c> (and the project is re-marked dirty so the report regenerates).
/// </summary>
public sealed class VideoRetryCommand(VideoCortexDbContext db) : IVideoRetryCommand
{
    public async Task<RetryResult> RetryVideoAsync(int videoId, CancellationToken ct = default)
    {
        var video = await db.Videos.Include(v => v.Project).FirstOrDefaultAsync(v => v.Id == videoId, ct);
        if (video is null) return RetryResult.Fail("Video not found.");

        video.Parked = false;
        video.RetryCount = 0;
        video.NextAttemptAt = null;
        video.LastError = null;

        if (string.IsNullOrWhiteSpace(video.TranscriptText))
        {
            // Includes NoTranscript — Apify may now have captions, so re-attempt the transcript.
            video.Status = VideoStatus.Added;
        }
        else if (string.IsNullOrWhiteSpace(video.SummaryBodyMd))
        {
            video.Status = VideoStatus.Transcribed;
        }
        else
        {
            video.Status = VideoStatus.Summarized;
            if (video.Project is not null)
                video.Project.ReportDirtySince ??= DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return RetryResult.Success();
    }
}
