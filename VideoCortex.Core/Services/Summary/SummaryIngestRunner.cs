using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Library;
using VideoCortex.Core.Services.Llm;

namespace VideoCortex.Core.Services.Summary;

/// <summary>
/// Summarizes one transcribed video, writes its OKF concept page, and advances it to
/// <see cref="VideoStatus.Summarized"/>. The page is written <b>before</b> the status flips — a
/// write failure leaves the row <c>Transcribed</c> to retry, so status never advances without a
/// page on disk. Failure applies <c>60s × 2^(n-1)</c> backoff (capped 1h) and parks after
/// <see cref="SummarySettings.MaxRetryAttempts"/>.
/// </summary>
public sealed class SummaryIngestRunner(
    VideoCortexDbContext db,
    IVideoSummarizer summarizer,
    IOkfLibraryStore library,
    IOptions<SummarySettings> settings,
    ILogger<SummaryIngestRunner> logger) : ISummaryIngestRunner
{
    private static readonly TimeSpan BaseBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    private readonly SummarySettings _settings = settings.Value;

    public async Task<SummaryIngestResult> RunAsync(Video video, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(video.TranscriptText))
            throw new InvalidOperationException(
                $"SummaryIngestRunner called for video {video.Id} ({video.YoutubeVideoId}) with empty transcript.");

        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == video.ProjectId, ct)
            ?? throw new InvalidOperationException($"Video {video.Id} has no owning project {video.ProjectId}.");

        var sw = Stopwatch.StartNew();
        try
        {
            var summary = await summarizer.SummarizeAsync(
                video.Title ?? string.Empty, video.ChannelTitle ?? string.Empty,
                video.TranscriptText, project.AIInstructions, ct);

            // Write the page BEFORE advancing status — never a Summarized row without a page.
            var slug = await library.WriteConceptPageAsync(project, video, summary, ct);

            video.SummaryTitle = summary.Title;
            video.SummaryDescription = summary.Description;
            video.SummaryBodyMd = summary.BodyMarkdown;
            video.ConceptSlug = slug;
            video.SummarizedAt = DateTime.UtcNow;
            video.Status = VideoStatus.Summarized;
            video.RetryCount = 0;
            video.LastError = null;
            video.NextAttemptAt = null;

            await db.SaveChangesAsync(ct);
            sw.Stop();
            logger.LogInformation("Summary ingest video {VideoId} ({Yt}): summarized in {Elapsed}ms",
                video.Id, video.YoutubeVideoId, sw.ElapsedMilliseconds);
            return new SummaryIngestResult(SummaryIngestOutcome.Summarized, 0, null, (int)sw.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            return await HandleFailureAsync(video, ex, (int)sw.ElapsedMilliseconds, ct);
        }
    }

    private async Task<SummaryIngestResult> HandleFailureAsync(Video video, Exception ex, int elapsedMs, CancellationToken ct)
    {
        video.RetryCount++;
        video.LastError = ex.Message;

        if (video.RetryCount >= _settings.MaxRetryAttempts)
        {
            video.Parked = true;
            video.NextAttemptAt = null;
            await db.SaveChangesAsync(ct);
            logger.LogWarning(ex, "Summary ingest video {VideoId} ({Yt}): parked (retry={Retry})",
                video.Id, video.YoutubeVideoId, video.RetryCount);
            return new SummaryIngestResult(SummaryIngestOutcome.Parked, video.RetryCount, video.LastError, elapsedMs);
        }

        var backoff = TimeSpan.FromTicks(Math.Min(
            MaxBackoff.Ticks, BaseBackoff.Ticks * (long)Math.Pow(2, video.RetryCount - 1)));
        video.NextAttemptAt = DateTime.UtcNow.Add(backoff);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Summary ingest video {VideoId} ({Yt}): retry (retry={Retry})",
            video.Id, video.YoutubeVideoId, video.RetryCount);
        return new SummaryIngestResult(SummaryIngestOutcome.Retry, video.RetryCount, video.LastError, elapsedMs);
    }
}
