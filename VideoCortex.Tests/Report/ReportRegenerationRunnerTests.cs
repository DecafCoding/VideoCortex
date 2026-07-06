using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Library;
using VideoCortex.Core.Services.Llm;
using VideoCortex.Core.Services.Report;

namespace VideoCortex.Tests.Report;

public class ReportRegenerationRunnerTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fx = new();
    private readonly string _root = TestPaths.NewTempDir();

    public void Dispose()
    {
        _fx.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private sealed class FakeSynthesizer : IReportSynthesizer
    {
        public int Calls { get; private set; }
        public ReportSynthesisResult? Result { get; set; }
        public Exception? Throw { get; set; }

        public Task<ReportSynthesisResult> SynthesizeAsync(ReportSynthesisContext ctx, CancellationToken ct = default)
        {
            Calls++;
            if (Throw is not null) throw Throw;
            return Task.FromResult(Result!);
        }
    }

    private ReportRegenerationRunner NewRunner(VideoCortexDbContext db, IReportSynthesizer synth, int maxRetry = 3)
    {
        var store = new OkfLibraryStore(_root, TestPaths.OkfTemplatesDir());
        return new ReportRegenerationRunner(
            db, synth, store, Options.Create(new ReportSettings { MaxRetryAttempts = maxRetry }),
            NullLogger<ReportRegenerationRunner>.Instance);
    }

    private int SeedProject(bool withSummaries)
    {
        using var db = _fx.CreateContext();
        var project = new Project { Name = "Proj", Slug = "proj", CreatedAt = DateTime.UtcNow, ReportDirtySince = DateTime.UtcNow };
        for (var i = 0; i < 2; i++)
        {
            project.Videos.Add(new Video
            {
                YoutubeVideoId = $"vid{i:D8}xxx"[..11],
                Status = VideoStatus.Summarized,
                AddedAt = DateTime.UtcNow,
                SummaryBodyMd = withSummaries ? "## Body\n\ntext" : null,
                ConceptSlug = withSummaries ? $"vid-{i}" : null,
                Title = $"Vid {i}",
            });
        }
        db.Projects.Add(project);
        db.SaveChanges();
        // The library folder + a seed index.html must exist for the keep-prior-file assertions.
        return project.Id;
    }

    private static FakeSynthesizer OkSynth() =>
        new() { Result = new ReportSynthesisResult("desc", [new("Theme", "body text", ["vid-0", "vid-1"])]) };

    [Fact]
    public async Task No_Eligible_Summaries_Is_NoOp_And_Clears_Dirty()
    {
        var id = SeedProject(withSummaries: false);
        using var db = _fx.CreateContext();
        var synth = OkSynth();

        var result = await NewRunner(db, synth).RunForProjectAsync(id);

        result.Outcome.Should().Be(ReportRegenerationOutcome.NoOp);
        synth.Calls.Should().Be(0);
        db.Projects.Single(p => p.Id == id).ReportDirtySince.Should().BeNull();
    }

    [Fact]
    public async Task Two_Summaries_Regenerates_And_Publishes()
    {
        var id = SeedProject(withSummaries: true);
        using var db = _fx.CreateContext();
        // Seed the library so WriteReportAsync overwrites a real folder.
        await new OkfLibraryStore(_root, TestPaths.OkfTemplatesDir())
            .CreateLibraryAsync(db.Projects.Single(p => p.Id == id));
        var synth = OkSynth();

        var result = await NewRunner(db, synth).RunForProjectAsync(id);

        result.Outcome.Should().Be(ReportRegenerationOutcome.Regenerated);
        synth.Calls.Should().Be(1);

        using var verify = _fx.CreateContext();
        var project = verify.Projects.Single(p => p.Id == id);
        project.Status.Should().Be(ProjectStatus.Idle);
        project.ReportUpdatedAt.Should().NotBeNull();
        project.ReportDirtySince.Should().BeNull();
        verify.Videos.Where(v => v.ProjectId == id).Should().OnlyContain(v => v.Status == VideoStatus.Published);

        var html = File.ReadAllText(Path.Combine(_root, "Proj", "index.html"));
        // The item heading plus a per-item Sources line linking the seeded concept pages.
        html.Should().Contain("<h2>Theme</h2>").And.Contain("<strong>Sources:</strong>");
        html.Should().Contain("href=\"vid-0.html\"").And.Contain("href=\"vid-1.html\"");
    }

    [Fact]
    public async Task Synthesis_Failure_Keeps_Prior_Index_And_Sets_Error()
    {
        var id = SeedProject(withSummaries: true);
        using var db = _fx.CreateContext();
        await new OkfLibraryStore(_root, TestPaths.OkfTemplatesDir())
            .CreateLibraryAsync(db.Projects.Single(p => p.Id == id));

        // Overwrite index.html with a known-good sentinel, then fail synthesis.
        var indexPath = Path.Combine(_root, "Proj", "index.html");
        File.WriteAllText(indexPath, "PRIOR-GOOD-REPORT");
        var before = File.ReadAllBytes(indexPath);

        var synth = new FakeSynthesizer { Throw = new HttpRequestException("llm down") };
        var result = await NewRunner(db, synth, maxRetry: 3).RunForProjectAsync(id);

        result.Outcome.Should().Be(ReportRegenerationOutcome.Retry);
        File.ReadAllBytes(indexPath).Should().Equal(before, "a synthesis failure must never clobber a good report");

        using var verify = _fx.CreateContext();
        var project = verify.Projects.Single(p => p.Id == id);
        project.Status.Should().Be(ProjectStatus.Error);
        project.ReportNextAttemptAt.Should().NotBeNull();
        verify.Videos.Where(v => v.ProjectId == id).Should().OnlyContain(v => v.Status == VideoStatus.Summarized);
    }

    [Fact]
    public async Task Parks_After_Max_Retries()
    {
        var id = SeedProject(withSummaries: true);
        using var db = _fx.CreateContext();
        var project = db.Projects.Single(p => p.Id == id);
        project.ReportRetryCount = 2; // maxRetry=3 → next failure parks
        db.SaveChanges();

        var synth = new FakeSynthesizer { Throw = new HttpRequestException("still down") };
        var result = await NewRunner(db, synth, maxRetry: 3).RunForProjectAsync(id);

        result.Outcome.Should().Be(ReportRegenerationOutcome.Parked);
        using var verify = _fx.CreateContext();
        verify.Projects.Single(p => p.Id == id).ReportDirtySince.Should().BeNull(); // worker stops selecting it
    }
}
