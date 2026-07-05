using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Library;
using VideoCortex.Core.Services.Llm;
using VideoCortex.Core.Services.Summary;

namespace VideoCortex.Tests.Summary;

public class SummaryIngestRunnerTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fx = new();
    private readonly string _root = TestPaths.NewTempDir();

    public void Dispose()
    {
        _fx.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private sealed class FakeSummarizer : IVideoSummarizer
    {
        public VideoSummary? Result { get; set; }
        public Exception? Throw { get; set; }

        public Task<VideoSummary> SummarizeAsync(
            string title, string channel, string transcript, string? aiInstructions = null, CancellationToken ct = default)
        {
            if (Throw is not null) throw Throw;
            return Task.FromResult(Result!);
        }
    }

    private SummaryIngestRunner NewRunner(VideoCortexDbContext db, IVideoSummarizer summarizer, int maxRetry = 3)
    {
        var store = new OkfLibraryStore(_root, TestPaths.OkfTemplatesDir());
        return new SummaryIngestRunner(
            db, summarizer, store,
            Options.Create(new SummarySettings { MaxRetryAttempts = maxRetry }),
            NullLogger<SummaryIngestRunner>.Instance);
    }

    private int SeedTranscribedVideo(string? transcript = "[00:00] hello world")
    {
        using var db = _fx.CreateContext();
        var project = new Project { Name = "Proj", Slug = "proj", CreatedAt = DateTime.UtcNow };
        var video = new Video
        {
            YoutubeVideoId = "dQw4w9WgXcQ",
            Status = VideoStatus.Transcribed,
            AddedAt = DateTime.UtcNow,
            TranscribedAt = DateTime.UtcNow,
            TranscriptText = transcript,
            Title = "Vid",
        };
        project.Videos.Add(video);
        db.Projects.Add(project);
        db.SaveChanges();
        return video.Id;
    }

    private static VideoSummary Ok() =>
        new("A Title", "desc", new[] { "t1" }, "## Body\n\ntext");

    [Fact]
    public async Task Success_Writes_Page_And_Advances_To_Summarized()
    {
        var id = SeedTranscribedVideo();
        using var db = _fx.CreateContext();
        var video = db.Videos.Single(v => v.Id == id);

        var result = await NewRunner(db, new FakeSummarizer { Result = Ok() }).RunAsync(video);

        result.Outcome.Should().Be(SummaryIngestOutcome.Summarized);
        video.Status.Should().Be(VideoStatus.Summarized);
        video.ConceptSlug.Should().NotBeNullOrWhiteSpace();
        video.SummaryTitle.Should().Be("A Title");
        video.SummaryDescription.Should().Be("desc");
        video.SummaryBodyMd.Should().Contain("## Body");
        video.SummarizedAt.Should().NotBeNull();
        video.RetryCount.Should().Be(0);

        File.Exists(Path.Combine(_root, "Proj", video.ConceptSlug + ".html")).Should().BeTrue();
    }

    [Fact]
    public async Task Failure_Retries_Then_Parks_At_Max()
    {
        var id = SeedTranscribedVideo();
        using var db = _fx.CreateContext();
        var video = db.Videos.Single(v => v.Id == id);
        var runner = NewRunner(db, new FakeSummarizer { Throw = new InvalidOperationException("llm down") }, maxRetry: 2);

        var first = await runner.RunAsync(video);
        first.Outcome.Should().Be(SummaryIngestOutcome.Retry);
        video.Status.Should().Be(VideoStatus.Transcribed);
        video.RetryCount.Should().Be(1);
        video.Parked.Should().BeFalse();
        video.NextAttemptAt.Should().NotBeNull();
        video.LastError.Should().Be("llm down");

        var second = await runner.RunAsync(video);
        second.Outcome.Should().Be(SummaryIngestOutcome.Parked);
        video.Parked.Should().BeTrue();
        video.NextAttemptAt.Should().BeNull();
    }

    [Fact]
    public async Task Empty_Transcript_Throws()
    {
        var id = SeedTranscribedVideo(transcript: "   ");
        using var db = _fx.CreateContext();
        var video = db.Videos.Single(v => v.Id == id);

        var act = () => NewRunner(db, new FakeSummarizer { Result = Ok() }).RunAsync(video);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
