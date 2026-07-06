using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Report;
using VideoCortex.Workers;

namespace VideoCortex.Tests.Report;

public class ReportDebounceTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    private sealed class CountingRunner : IReportRegenerationRunner
    {
        public int Calls { get; private set; }
        public Task<ReportRegenerationResult> RunForProjectAsync(int projectId, CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(new ReportRegenerationResult(ReportRegenerationOutcome.Regenerated, 0, null));
        }
    }

    private (ReportWorker worker, CountingRunner runner) BuildWorker()
    {
        var runner = new CountingRunner();
        var services = new ServiceCollection();
        services.AddScoped(_ => new VideoCortexDbContext(_fx.Options));
        services.AddScoped<IReportRegenerationRunner>(_ => runner);
        var provider = services.BuildServiceProvider();

        var worker = new ReportWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new TestOptionsMonitor<ReportSettings>(new ReportSettings { CoalesceDebounceSeconds = 0 }),
            NullLogger<ReportWorker>.Instance);
        return (worker, runner);
    }

    private int SeedProject(DateTime? dirtySince)
    {
        using var db = _fx.CreateContext();
        var p = new Project { Name = "P", Slug = "p", CreatedAt = DateTime.UtcNow, ReportDirtySince = dirtySince };
        db.Projects.Add(p);
        db.SaveChanges();
        return p.Id;
    }

    [Fact]
    public async Task Dirty_Project_Is_Selected_And_Run_Once()
    {
        SeedProject(DateTime.UtcNow.AddMinutes(-1)); // dirty, past debounce
        var (worker, runner) = BuildWorker();

        var didWork = await worker.TickOnceAsync(CancellationToken.None);

        didWork.Should().BeTrue();
        runner.Calls.Should().Be(1); // one project = one invocation (coalesced by the single timestamp)
    }

    [Fact]
    public async Task Clean_Project_Is_Not_Selected()
    {
        SeedProject(dirtySince: null);
        var (worker, runner) = BuildWorker();

        var didWork = await worker.TickOnceAsync(CancellationToken.None);

        didWork.Should().BeFalse();
        runner.Calls.Should().Be(0);
    }
}
