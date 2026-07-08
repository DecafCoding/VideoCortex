using FluentAssertions;
using VideoCortex.Core.Entities;
using VideoCortex.Features.Diagnostics.Services;

namespace VideoCortex.Tests.Diagnostics;

/// <summary>Query behavior of the Diagnostics page's read service.</summary>
public class DiagnosticsServiceTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    private void Seed()
    {
        using var db = _fx.CreateContext();

        var healthy = new Project { Name = "Healthy", Slug = "healthy", CreatedAt = DateTime.UtcNow };
        healthy.Videos.Add(new Video { YoutubeVideoId = "okvideo0001", Status = VideoStatus.Published, AddedAt = DateTime.UtcNow });

        var errored = new Project
        {
            Name = "Errored",
            Slug = "errored",
            CreatedAt = DateTime.UtcNow,
            Status = ProjectStatus.Error,
            ReportRetryCount = 2,
            LastReportError = "llm down",
        };
        errored.Videos.Add(new Video
        {
            YoutubeVideoId = "badvideo001",
            Status = VideoStatus.Added,
            Parked = true,
            RetryCount = 3,
            LastError = "transcript fetch failed",
            AddedAt = DateTime.UtcNow,
        });
        errored.Videos.Add(new Video
        {
            YoutubeVideoId = "notranscri1",
            Status = VideoStatus.NoTranscript,
            AddedAt = DateTime.UtcNow,
        });

        db.Projects.AddRange(healthy, errored);
        db.SaveChanges();
    }

    [Fact]
    public async Task GetProjectIssuesAsync_ErroredProjectsOnly_ReturnsErrorDetails()
    {
        Seed();
        using var db = _fx.CreateContext();

        var issues = await new DiagnosticsService(db).GetProjectIssuesAsync();

        issues.Should().ContainSingle();
        var issue = issues[0];
        issue.Name.Should().Be("Errored");
        issue.Status.Should().Be(ProjectStatus.Error);
        issue.ReportRetryCount.Should().Be(2);
        issue.LastReportError.Should().Be("llm down");
        issue.IsParked.Should().BeTrue("Error status with no dirty timestamp means retries are exhausted");
    }

    [Fact]
    public async Task GetVideoIssuesAsync_FailedOrParkedVideos_ReturnsProjectContext()
    {
        Seed();
        using var db = _fx.CreateContext();

        var issues = await new DiagnosticsService(db).GetVideoIssuesAsync();

        issues.Should().HaveCount(2);
        issues.Should().OnlyContain(v => v.ProjectSlug == "errored");
        issues.Should().ContainSingle(v => v.Parked && v.LastError == "transcript fetch failed");
        issues.Should().ContainSingle(v => v.Status == VideoStatus.NoTranscript);
    }
}
