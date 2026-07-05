using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Config;

namespace VideoCortex.Core.Services.Transcripts;

/// <summary>
/// Applies an Apify <see cref="Transcript"/> to a <see cref="Video"/> row: backfills metadata,
/// transitions status (<c>Transcribed</c>/<c>NoTranscript</c>), and on failure applies
/// exponential backoff (<c>60s × 2^(n-1)</c>, capped at 1h) then parks after
/// <see cref="TranscriptWorkerSettings.MaxRetryAttempts"/>.
/// </summary>
public sealed class TranscriptIngestRunner(
    VideoCortexDbContext db,
    ITranscriptSource transcripts,
    IOptions<TranscriptWorkerSettings> settings,
    ILogger<TranscriptIngestRunner> logger) : ITranscriptIngestRunner
{
    private static readonly TimeSpan BaseBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    private readonly TranscriptWorkerSettings _settings = settings.Value;

    public async Task<TranscriptIngestResult> RunAsync(Video video, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        Transcript transcript;
        try
        {
            transcript = await transcripts.FetchAsync(video.YoutubeVideoId, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Transcript fetch threw for video {VideoId}", video.YoutubeVideoId);
            transcript = Transcript.Failure(ex.Message);
        }

        sw.Stop();
        var elapsedMs = (int)sw.ElapsedMilliseconds;

        if (!transcript.Success)
            return await HandleFailureAsync(video, transcript, elapsedMs, ct);

        return await HandleSuccessAsync(video, transcript, elapsedMs, ct);
    }

    private async Task<TranscriptIngestResult> HandleFailureAsync(
        Video video, Transcript transcript, int elapsedMs, CancellationToken ct)
    {
        video.RetryCount++;
        video.LastError = transcript.ErrorMessage ?? "unknown error";

        if (video.RetryCount >= _settings.MaxRetryAttempts)
        {
            video.Parked = true;
            video.NextAttemptAt = null;
            await db.SaveChangesAsync(ct);
            logger.LogInformation(
                "Transcript ingest video {VideoId} ({Yt}): parked (retry={Retry}) in {Elapsed}ms",
                video.Id, video.YoutubeVideoId, video.RetryCount, elapsedMs);
            return new TranscriptIngestResult(TranscriptIngestOutcome.Parked, video.RetryCount, video.LastError, elapsedMs);
        }

        // Backoff: 60s × 2^(RetryCount-1), capped at 1h. Overflow-safe via ticks.
        var backoff = TimeSpan.FromTicks(Math.Min(
            MaxBackoff.Ticks,
            BaseBackoff.Ticks * (long)Math.Pow(2, video.RetryCount - 1)));
        video.NextAttemptAt = DateTime.UtcNow.Add(backoff);
        // Status stays Added — the worker re-queries once NextAttemptAt passes.
        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Transcript ingest video {VideoId} ({Yt}): retry (retry={Retry}) in {Elapsed}ms",
            video.Id, video.YoutubeVideoId, video.RetryCount, elapsedMs);
        return new TranscriptIngestResult(TranscriptIngestOutcome.Retry, video.RetryCount, video.LastError, elapsedMs);
    }

    private async Task<TranscriptIngestResult> HandleSuccessAsync(
        Video video, Transcript transcript, int elapsedMs, CancellationToken ct)
    {
        // Overwrite metadata only when Apify returned a non-null value.
        if (transcript.Title is not null) video.Title = transcript.Title;
        if (transcript.ChannelTitle is not null) video.ChannelTitle = transcript.ChannelTitle;
        if (transcript.Description is not null) video.Description = transcript.Description;
        if (transcript.DurationSeconds is not null) video.DurationSeconds = transcript.DurationSeconds;
        if (transcript.ViewCount is not null) video.ViewCount = transcript.ViewCount;
        if (transcript.LikeCount is not null) video.LikeCount = transcript.LikeCount;
        if (transcript.CommentsCount is not null) video.CommentsCount = transcript.CommentsCount;
        if (!string.IsNullOrEmpty(transcript.ThumbnailUrl)) video.ThumbnailUrl = transcript.ThumbnailUrl;

        if (transcript.HasTranscript)
        {
            video.TranscriptText = transcript.TranscriptText;
            video.TranscriptLang = transcript.TranscriptLang;
            video.TranscribedAt = DateTime.UtcNow;
            video.Status = VideoStatus.Transcribed;
        }
        else
        {
            video.TranscriptText = null;
            video.TranscriptLang = null;
            video.Status = VideoStatus.NoTranscript;
        }

        // Status advanced → reset retry state.
        video.RetryCount = 0;
        video.LastError = null;
        video.NextAttemptAt = null;

        var outcome = video.Status == VideoStatus.Transcribed
            ? TranscriptIngestOutcome.Transcribed
            : TranscriptIngestOutcome.NoTranscript;

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Transcript ingest video {VideoId} ({Yt}): {Outcome} in {Elapsed}ms",
            video.Id, video.YoutubeVideoId, outcome, elapsedMs);
        return new TranscriptIngestResult(outcome, 0, null, elapsedMs);
    }
}
