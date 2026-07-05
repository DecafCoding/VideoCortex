using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Summary;

namespace VideoCortex.Workers;

/// <summary>
/// Polls for <c>Transcribed</c>, un-parked, due videos and hands each to the summary ingest
/// runner. Singleton hosted service; opens a fresh DI scope per tick for the scoped
/// <see cref="VideoCortexDbContext"/> and runner.
/// </summary>
public sealed class SummaryWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<SummarySettings> monitor,
    ILogger<SummaryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SummaryWorker starting. idlePoll={IdlePollSeconds}s",
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
                logger.LogError(ex, "SummaryWorker tick threw");
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

        logger.LogInformation("SummaryWorker stopping.");
    }

    private async Task<bool> TickOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideoCortexDbContext>();
        var runner = scope.ServiceProvider.GetRequiredService<ISummaryIngestRunner>();

        var now = DateTime.UtcNow;
        var video = await db.Videos
            .Where(v => v.Status == VideoStatus.Transcribed
                && !v.Parked
                && (v.NextAttemptAt == null || v.NextAttemptAt <= now))
            .OrderBy(v => v.NextAttemptAt)
            .ThenBy(v => v.TranscribedAt)
            .FirstOrDefaultAsync(ct);

        if (video is null) return false;

        await runner.RunAsync(video, ct);
        return true;
    }
}
