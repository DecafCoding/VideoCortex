using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Db;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Report;

namespace VideoCortex.Workers;

/// <summary>
/// Debounced polling service that regenerates project reports. Picks the project with the oldest
/// <c>ReportDirtySince</c> that is past its backoff, waits out <c>CoalesceDebounceSeconds</c> from
/// that timestamp so a burst of adds collapses into one synthesis, then runs the regeneration
/// runner. Single-user app → one instance, no concurrency.
/// </summary>
public sealed class ReportWorker(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<ReportSettings> monitor,
    ILogger<ReportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var s = monitor.CurrentValue;
        logger.LogInformation("ReportWorker starting. idlePoll={IdlePollSeconds}s debounce={DebounceSeconds}s",
            s.IdlePollSeconds, s.CoalesceDebounceSeconds);

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
                logger.LogError(ex, "ReportWorker tick threw");
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

        logger.LogInformation("ReportWorker stopping.");
    }

    internal async Task<bool> TickOnceAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VideoCortexDbContext>();
        var runner = scope.ServiceProvider.GetRequiredService<IReportRegenerationRunner>();

        var now = DateTime.UtcNow;
        var project = await db.Projects
            .Where(p => p.ReportDirtySince != null
                && (p.ReportNextAttemptAt == null || p.ReportNextAttemptAt <= now))
            .OrderBy(p => p.ReportNextAttemptAt)
            .ThenBy(p => p.ReportDirtySince)
            .Select(p => new { p.Id, p.ReportDirtySince })
            .FirstOrDefaultAsync(ct);

        if (project is null) return false;

        // Debounce from the dirty timestamp so late-arriving summaries in a burst are included.
        var debounce = TimeSpan.FromSeconds(monitor.CurrentValue.CoalesceDebounceSeconds);
        var elapsed = DateTime.UtcNow - project.ReportDirtySince!.Value;
        if (elapsed < debounce)
            await Task.Delay(debounce - elapsed, ct);

        await runner.RunForProjectAsync(project.Id, ct);
        return true;
    }
}
