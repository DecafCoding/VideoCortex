using FluentAssertions;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Triage;

namespace VideoCortex.Tests.Videos;

public class VideoRetryCommandTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fx = new();

    public void Dispose() => _fx.Dispose();

    private int SeedVideo(Action<Video> configure)
    {
        using var db = _fx.CreateContext();
        var project = new Project { Name = "P", Slug = "p", CreatedAt = DateTime.UtcNow };
        var video = new Video
        {
            YoutubeVideoId = "dQw4w9WgXcQ",
            AddedAt = DateTime.UtcNow,
            Parked = true,
            RetryCount = 3,
            LastError = "boom",
            NextAttemptAt = DateTime.UtcNow.AddHours(1),
        };
        configure(video);
        project.Videos.Add(video);
        db.Projects.Add(project);
        db.SaveChanges();
        return video.Id;
    }

    private Video RunAndReload(int id)
    {
        using (var db = _fx.CreateContext())
        {
            var result = new VideoRetryCommand(db).RetryVideoAsync(id).GetAwaiter().GetResult();
            result.Ok.Should().BeTrue();
        }
        using var verify = _fx.CreateContext();
        return verify.Videos.Single(v => v.Id == id);
    }

    [Fact]
    public void Parked_At_Transcript_Routes_To_Added_And_Clears_Fields()
    {
        var id = SeedVideo(v => { v.Status = VideoStatus.Error; v.TranscriptText = null; });
        var v = RunAndReload(id);

        v.Status.Should().Be(VideoStatus.Added);
        v.Parked.Should().BeFalse();
        v.RetryCount.Should().Be(0);
        v.NextAttemptAt.Should().BeNull();
        v.LastError.Should().BeNull();
    }

    [Fact]
    public void Parked_At_Summary_Routes_To_Transcribed()
    {
        var id = SeedVideo(v => { v.Status = VideoStatus.Error; v.TranscriptText = "[00:00] hi"; v.SummaryBodyMd = null; });
        RunAndReload(id).Status.Should().Be(VideoStatus.Transcribed);
    }

    [Fact]
    public void Summarized_Routes_To_Summarized_And_Marks_Project_Dirty()
    {
        var id = SeedVideo(v =>
        {
            v.Status = VideoStatus.Summarized;
            v.TranscriptText = "[00:00] hi";
            v.SummaryBodyMd = "## Body";
        });
        var v = RunAndReload(id);

        v.Status.Should().Be(VideoStatus.Summarized);
        using var verify = _fx.CreateContext();
        verify.Projects.Single().ReportDirtySince.Should().NotBeNull();
    }

    [Fact]
    public void NoTranscript_Routes_To_Added()
    {
        var id = SeedVideo(v => { v.Status = VideoStatus.NoTranscript; v.TranscriptText = null; });
        RunAndReload(id).Status.Should().Be(VideoStatus.Added);
    }

    [Fact]
    public async Task Unknown_Id_Returns_Fail()
    {
        using var db = _fx.CreateContext();
        var result = await new VideoRetryCommand(db).RetryVideoAsync(9999);
        result.Ok.Should().BeFalse();
    }
}
