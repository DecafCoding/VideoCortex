using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Triage;

namespace VideoCortex.Tests.Core.Triage;

public class VideoCommandsTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    private static VideoCommands NewCommands(VideoCortexDbContext db)
        => new(db, NullLogger<VideoCommands>.Instance);

    private static int SeedProject(VideoCortexDbContext db, string slug)
    {
        var p = new Project { Name = slug, Slug = slug, CreatedAt = DateTime.UtcNow };
        db.Projects.Add(p);
        db.SaveChanges();
        return p.Id;
    }

    [Fact]
    public async Task AddValidUrl_Persists_Added_Video()
    {
        using var db = _fx.CreateContext();
        var projectId = SeedProject(db, "p1");

        var result = await NewCommands(db).AddVideoByUrlAsync(projectId, "https://youtu.be/dQw4w9WgXcQ");

        result.Outcome.Should().Be(AddVideoOutcome.Added);
        result.YoutubeVideoId.Should().Be("dQw4w9WgXcQ");

        using var verify = _fx.CreateContext();
        var video = verify.Videos.Single();
        video.ProjectId.Should().Be(projectId);
        video.YoutubeVideoId.Should().Be("dQw4w9WgXcQ");
        video.Status.Should().Be(VideoStatus.Added);
        video.AddedAt.Should().NotBe(default);
        video.Title.Should().BeNull(); // metadata deferred to Phase 4
    }

    [Fact]
    public async Task AddSameId_Twice_In_Project_Returns_Duplicate_And_One_Row()
    {
        using var db = _fx.CreateContext();
        var projectId = SeedProject(db, "p1");
        var cmd = NewCommands(db);

        (await cmd.AddVideoByUrlAsync(projectId, "dQw4w9WgXcQ")).Outcome.Should().Be(AddVideoOutcome.Added);
        var second = await cmd.AddVideoByUrlAsync(projectId, "https://www.youtube.com/watch?v=dQw4w9WgXcQ");

        second.Outcome.Should().Be(AddVideoOutcome.DuplicateInProject);
        using var verify = _fx.CreateContext();
        verify.Videos.Count(v => v.ProjectId == projectId).Should().Be(1);
    }

    [Fact]
    public async Task SameId_In_Two_Projects_Yields_Two_Rows()
    {
        using var db = _fx.CreateContext();
        var p1 = SeedProject(db, "p1");
        var p2 = SeedProject(db, "p2");
        var cmd = NewCommands(db);

        (await cmd.AddVideoByUrlAsync(p1, "dQw4w9WgXcQ")).Success.Should().BeTrue();
        (await cmd.AddVideoByUrlAsync(p2, "dQw4w9WgXcQ")).Success.Should().BeTrue();

        using var verify = _fx.CreateContext();
        verify.Videos.Count().Should().Be(2);
    }

    [Fact]
    public async Task InvalidInput_Returns_InvalidUrl_And_Inserts_Nothing()
    {
        using var db = _fx.CreateContext();
        var projectId = SeedProject(db, "p1");

        var result = await NewCommands(db).AddVideoByUrlAsync(projectId, "not a url");

        result.Outcome.Should().Be(AddVideoOutcome.InvalidUrl);
        using var verify = _fx.CreateContext();
        verify.Videos.Should().BeEmpty();
    }

    [Fact]
    public async Task UnknownProject_Returns_InvalidUrl_And_Inserts_Nothing()
    {
        using var db = _fx.CreateContext();
        // No project seeded.

        var result = await NewCommands(db).AddVideoByUrlAsync(999, "dQw4w9WgXcQ");

        result.Outcome.Should().Be(AddVideoOutcome.InvalidUrl);
        using var verify = _fx.CreateContext();
        verify.Videos.Should().BeEmpty();
    }
}
