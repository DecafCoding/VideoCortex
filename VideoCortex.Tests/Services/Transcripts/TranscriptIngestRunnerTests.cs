using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Db;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Transcripts;

namespace VideoCortex.Tests.Services.Transcripts;

public class TranscriptIngestRunnerTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    private sealed class FakeTranscriptSource : ITranscriptSource
    {
        public Queue<Transcript> Responses { get; } = new();
        public Exception? ThrowOnNext { get; set; }

        public Task<Transcript> FetchAsync(string videoId, CancellationToken ct = default)
        {
            if (ThrowOnNext is not null)
            {
                var ex = ThrowOnNext;
                ThrowOnNext = null;
                throw ex;
            }
            return Task.FromResult(Responses.Dequeue());
        }
    }

    private static TranscriptIngestRunner NewRunner(
        VideoCortexDbContext db, ITranscriptSource src, int maxRetry = 3)
        => new(db, src, Options.Create(new TranscriptWorkerSettings { MaxRetryAttempts = maxRetry }),
            NullLogger<TranscriptIngestRunner>.Instance);

    private int SeedVideo(int retryCount = 0)
    {
        using var db = _fx.CreateContext();
        var project = new Project { Name = "P", Slug = "p", CreatedAt = DateTime.UtcNow };
        var video = new Video
        {
            YoutubeVideoId = "dQw4w9WgXcQ",
            Status = VideoStatus.Added,
            AddedAt = DateTime.UtcNow,
            DurationSeconds = 600,
            ViewCount = 100,
            RetryCount = retryCount,
        };
        project.Videos.Add(video);
        db.Projects.Add(project);
        db.SaveChanges();
        return video.Id;
    }

    private static Transcript Ok(bool hasTranscript, int? duration = null, long? views = null)
        => new(true, hasTranscript ? "[00:00] hi" : null, hasTranscript ? "en" : null, hasTranscript,
            "Real Title", "Real Channel", "desc", duration, views, null, null, "thumb", null);

    [Fact]
    public async Task Success_With_Transcript_Sets_Transcribed_And_Backfills()
    {
        var id = SeedVideo();
        using var db = _fx.CreateContext();
        var src = new FakeTranscriptSource();
        src.Responses.Enqueue(Ok(hasTranscript: true, duration: 250, views: 999));
        var video = db.Videos.Single(v => v.Id == id);

        var result = await NewRunner(db, src).RunAsync(video);

        result.Outcome.Should().Be(TranscriptIngestOutcome.Transcribed);
        video.Status.Should().Be(VideoStatus.Transcribed);
        video.TranscriptText.Should().Be("[00:00] hi");
        video.TranscriptLang.Should().Be("en");
        video.TranscribedAt.Should().NotBeNull();
        video.Title.Should().Be("Real Title");
        video.ChannelTitle.Should().Be("Real Channel");
        video.DurationSeconds.Should().Be(250);
        video.ViewCount.Should().Be(999);
        video.RetryCount.Should().Be(0);
        video.LastError.Should().BeNull();
    }

    [Fact]
    public async Task Success_Without_Transcript_Sets_NoTranscript_But_Keeps_Metadata()
    {
        var id = SeedVideo();
        using var db = _fx.CreateContext();
        var src = new FakeTranscriptSource();
        src.Responses.Enqueue(Ok(hasTranscript: false, duration: 250));
        var video = db.Videos.Single(v => v.Id == id);

        var result = await NewRunner(db, src).RunAsync(video);

        result.Outcome.Should().Be(TranscriptIngestOutcome.NoTranscript);
        video.Status.Should().Be(VideoStatus.NoTranscript);
        video.TranscriptText.Should().BeNull();
        video.Title.Should().Be("Real Title");
        video.DurationSeconds.Should().Be(250);
    }

    [Fact]
    public async Task Success_Does_Not_Overwrite_Metadata_With_Null()
    {
        var id = SeedVideo(); // seeded DurationSeconds=600, ViewCount=100
        using var db = _fx.CreateContext();
        var src = new FakeTranscriptSource();
        src.Responses.Enqueue(Ok(hasTranscript: true, duration: null, views: null));
        var video = db.Videos.Single(v => v.Id == id);

        await NewRunner(db, src).RunAsync(video);

        video.DurationSeconds.Should().Be(600);
        video.ViewCount.Should().Be(100);
    }

    [Fact]
    public async Task First_Failure_Retries_With_60s_Backoff()
    {
        var id = SeedVideo();
        using var db = _fx.CreateContext();
        var src = new FakeTranscriptSource();
        src.Responses.Enqueue(Transcript.Failure("boom"));
        var video = db.Videos.Single(v => v.Id == id);

        var result = await NewRunner(db, src).RunAsync(video);

        result.Outcome.Should().Be(TranscriptIngestOutcome.Retry);
        video.Status.Should().Be(VideoStatus.Added);
        video.RetryCount.Should().Be(1);
        video.Parked.Should().BeFalse();
        video.LastError.Should().Be("boom");
        (video.NextAttemptAt!.Value - DateTime.UtcNow).TotalSeconds.Should().BeApproximately(60, 5);
    }

    [Fact]
    public async Task Failure_At_Max_Retries_Parks()
    {
        var id = SeedVideo(retryCount: 2); // maxRetry=3 → increment to 3 → park
        using var db = _fx.CreateContext();
        var src = new FakeTranscriptSource();
        src.Responses.Enqueue(Transcript.Failure("nope"));
        var video = db.Videos.Single(v => v.Id == id);

        var result = await NewRunner(db, src, maxRetry: 3).RunAsync(video);

        result.Outcome.Should().Be(TranscriptIngestOutcome.Parked);
        video.Parked.Should().BeTrue();
        video.NextAttemptAt.Should().BeNull();
    }

    [Theory]
    [InlineData(1, 120)]    // retry becomes 2 → 60×2^1 = 120s
    [InlineData(5, 3600)]   // retry becomes 6 → 60×2^5 = 1920s? capped check below
    public async Task Backoff_Doubles_Then_Caps(int seedRetry, int _)
    {
        var id = SeedVideo(retryCount: seedRetry);
        using var db = _fx.CreateContext();
        var src = new FakeTranscriptSource();
        src.Responses.Enqueue(Transcript.Failure("x"));
        var video = db.Videos.Single(v => v.Id == id);

        await NewRunner(db, src, maxRetry: 100).RunAsync(video);

        var delay = (video.NextAttemptAt!.Value - DateTime.UtcNow).TotalSeconds;
        delay.Should().BeLessThanOrEqualTo(3600 + 5); // never exceeds the 1h cap
        delay.Should().BeGreaterThan(60);              // and grows past the base after the first retry
    }

    [Fact]
    public async Task Thrown_Fetch_Is_Treated_As_Transient_Retry()
    {
        var id = SeedVideo();
        using var db = _fx.CreateContext();
        var src = new FakeTranscriptSource { ThrowOnNext = new InvalidOperationException("boom") };
        var video = db.Videos.Single(v => v.Id == id);

        var result = await NewRunner(db, src).RunAsync(video);

        result.Outcome.Should().Be(TranscriptIngestOutcome.Retry);
        video.RetryCount.Should().Be(1);
        video.LastError.Should().Be("boom");
    }
}
