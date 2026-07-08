using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Library;
using VideoCortex.Core.Services.Llm;

namespace VideoCortex.Core.Services.Report;

/// <summary>
/// Regenerates one project's report: gate on summary presence → synthesize → write the root
/// <c>index.html</c> → flip folded videos to <see cref="VideoStatus.Published"/>. A synthesis or
/// write failure <b>keeps the prior good file</b> (WriteReportAsync is never called on the failure
/// path), records <see cref="ProjectStatus.Error"/>, and applies <c>60s × 2^(n-1)</c> backoff
/// (capped 1h), parking after <see cref="ReportSettings.MaxRetryAttempts"/>.
/// </summary>
public sealed class ReportRegenerationRunner(
    VideoCortexDbContext db,
    IReportSynthesizer synthesizer,
    IOkfLibraryStore library,
    IOptions<ReportSettings> settings,
    ILogger<ReportRegenerationRunner> logger) : IReportRegenerationRunner
{
    private static readonly TimeSpan BaseBackoff = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromHours(1);

    private readonly ReportSettings _settings = settings.Value;

    public async Task<ReportRegenerationResult> RunForProjectAsync(int projectId, CancellationToken ct = default)
    {
        var project = await db.Projects.Include(p => p.Videos).FirstOrDefaultAsync(p => p.Id == projectId, ct);
        if (project is null)
            return new ReportRegenerationResult(ReportRegenerationOutcome.NoOp, 0, "project not found");

        // Gate: only videos with a written summary + concept page are eligible.
        var eligible = project.Videos
            .Where(v => !string.IsNullOrWhiteSpace(v.SummaryBodyMd)
                && !string.IsNullOrWhiteSpace(v.ConceptSlug)
                && (v.Status == VideoStatus.Summarized || v.Status == VideoStatus.Published))
            .ToList();

        if (eligible.Count == 0)
        {
            // Nothing to synthesize — clear the dirty flag without an LLM call.
            ClearScheduling(project);
            await db.SaveChangesAsync(ct);
            return new ReportRegenerationResult(ReportRegenerationOutcome.NoOp, 0, null);
        }

        project.Status = ProjectStatus.Updating;
        await db.SaveChangesAsync(ct);

        try
        {
            var ctx = new ReportSynthesisContext(
                project.Name, project.AIInstructions,
                eligible.Select(v => new VideoSummaryInput(
                    v.Id, v.YoutubeVideoId, v.Title ?? v.YoutubeVideoId, v.ChannelTitle,
                    v.ConceptSlug!, v.SummaryTitle, v.SummaryDescription, v.SummaryBodyMd)).ToList());

            var result = await synthesizer.SynthesizeAsync(ctx, ct);

            // Map each eligible video's concept slug to a display title so the report can render
            // readable per-item "Sources" links (and only ever link real concept pages).
            var sources = eligible
                .Select(v => new ReportSource(v.ConceptSlug!, v.SummaryTitle ?? v.Title ?? v.YoutubeVideoId))
                .ToList();

            // Success — write the report, then fold videos in. (Write before status flip; a write
            // failure throws and routes to the failure path without advancing anything.)
            await library.WriteReportAsync(project, result.LibraryDescription, result.Items, sources, ct);

            var folded = 0;
            var now = DateTime.UtcNow;
            foreach (var v in eligible.Where(v => v.Status == VideoStatus.Summarized))
            {
                v.Status = VideoStatus.Published;
                v.PublishedAt = now;
                folded++;
            }

            project.Status = ProjectStatus.Idle;
            project.ReportUpdatedAt = now;
            ClearScheduling(project);
            project.ReportRetryCount = 0;
            project.LastReportError = null;
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Report regen project {ProjectId}: regenerated, folded {Folded} video(s)",
                projectId, folded);
            return new ReportRegenerationResult(ReportRegenerationOutcome.Regenerated, folded, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return await HandleFailureAsync(project, ex, ct);
        }
    }

    private async Task<ReportRegenerationResult> HandleFailureAsync(Project project, Exception ex, CancellationToken ct)
    {
        project.Status = ProjectStatus.Error;
        project.ReportRetryCount++;
        project.LastReportError = ex.Message;

        if (project.ReportRetryCount >= _settings.MaxRetryAttempts)
        {
            // Park: stop the worker selecting it (dirty cleared) until a manual Rebuild re-dirties.
            project.ReportDirtySince = null;
            project.ReportNextAttemptAt = null;
            await db.SaveChangesAsync(ct);
            logger.LogWarning(ex, "Report regen project {ProjectId}: parked (retry={Retry})",
                project.Id, project.ReportRetryCount);
            return new ReportRegenerationResult(ReportRegenerationOutcome.Parked, 0, ex.Message);
        }

        var backoff = TimeSpan.FromTicks(Math.Min(
            MaxBackoff.Ticks, BaseBackoff.Ticks * (long)Math.Pow(2, project.ReportRetryCount - 1)));
        project.ReportNextAttemptAt = DateTime.UtcNow.Add(backoff); // dirty stays set → retried after backoff
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Report regen project {ProjectId}: retry (retry={Retry})",
            project.Id, project.ReportRetryCount);
        return new ReportRegenerationResult(ReportRegenerationOutcome.Retry, 0, ex.Message);
    }

    private static void ClearScheduling(Project project)
    {
        project.ReportDirtySince = null;
        project.ReportNextAttemptAt = null;
    }
}
