using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Transcripts;

namespace VideoCortex.Workers;

/// <summary>
/// Polls for <c>Added</c>, un-parked, due videos and hands each to the transcript ingest
/// runner. A singleton hosted service that opens a fresh DI scope per tick (so it can use the
/// scoped <see cref="VideoCortexDbContext"/> and runner without a captive dependency).
/// </summary>
public sealed class TranscriptWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<TranscriptWorkerSettings> monitor,
    ILogger<TranscriptWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("TranscriptWorker starting. idlePoll={IdlePollSeconds}s",
            monitor.CurrentValue.IdlePollSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            bool didWork;
            try
            {
                didWork = await TickOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                logger.LogError(ex, "TranscriptWorker tick threw");
                didWork = false;
            }

            if (!didWork)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(monitor.CurrentValue.IdlePollSeconds), stoppingToken);
                }
                catch (OperationCanceledException) { break; }
            }
        }

        logger.LogInformation("TranscriptWorker stopping.");
    }

    // true = processed a row (loop again immediately); false = queue empty (loop should sleep).
    private async Task<bool> TickOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideoCortexDbContext>();
        var runner = scope.ServiceProvider.GetRequiredService<ITranscriptIngestRunner>();

        var now = DateTime.UtcNow;
        var video = await db.Videos
            .Where(v => v.Status == VideoStatus.Added
                && !v.Parked
                && (v.NextAttemptAt == null || v.NextAttemptAt <= now))
            .OrderBy(v => v.NextAttemptAt)
            .ThenBy(v => v.AddedAt)
            .FirstOrDefaultAsync(ct);

        if (video is null) return false;

        await runner.RunAsync(video, ct);
        return true;
    }
}
