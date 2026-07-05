using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Transcripts;

namespace VideoCortex.Tests.Services.Transcripts;

public class ApifyTranscriptSourceTests
{
    private static string CapturedPayload()
        => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", "apify-scraper-item.json"));

    private static ApifyTranscriptSource Source(FakeHttpMessageHandler handler, string token = "test-token")
    {
        var http = new HttpClient(handler);
        var settings = Options.Create(new ApifySettings { Token = token });
        return new ApifyTranscriptSource(http, settings, NullLogger<ApifyTranscriptSource>.Instance);
    }

    [Fact]
    public async Task Success_With_Subtitles_Returns_Transcript_And_Metadata()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, CapturedPayload());
        var result = await Source(handler).FetchAsync("dQw4w9WgXcQ");

        result.Success.Should().BeTrue();
        result.HasTranscript.Should().BeTrue();
        result.TranscriptText.Should().Contain("[00:00] hello").And.Contain("[00:02] world");
        result.Title.Should().Be("Never Gonna Give You Up");
        result.ChannelTitle.Should().Be("Rick Astley");
        result.DurationSeconds.Should().Be(253);
        result.ViewCount.Should().Be(1600000000);
        result.TranscriptLang.Should().Be("en");

        // The request went to the Apify run-sync endpoint.
        handler.CallCount.Should().Be(1);
        handler.LastRequestUri!.ToString().Should().Contain("run-sync-get-dataset-items");
    }

    [Fact]
    public async Task Success_Without_Subtitles_Sets_Metadata_But_No_Transcript()
    {
        const string noSubs = """
            [{ "title": "T", "channelName": "C", "duration": "PT1M", "viewCount": 5, "subtitles": [] }]
            """;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, noSubs);
        var result = await Source(handler).FetchAsync("dQw4w9WgXcQ");

        result.Success.Should().BeTrue();
        result.HasTranscript.Should().BeFalse();
        result.TranscriptText.Should().BeNull();
        result.Title.Should().Be("T");
        result.DurationSeconds.Should().Be(60);
    }

    [Fact]
    public async Task Missing_Token_Fails_Without_Calling_Apify()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, CapturedPayload());
        var result = await Source(handler, token: "").FetchAsync("dQw4w9WgXcQ");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("token");
        handler.CallCount.Should().Be(0); // no wasted/paid call
    }

    [Fact]
    public async Task Non2xx_Fails_With_Status_And_Body()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.TooManyRequests, "rate limited");
        var result = await Source(handler).FetchAsync("dQw4w9WgXcQ");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("429").And.Contain("rate limited");
    }

    [Fact]
    public async Task Empty_Dataset_Fails()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "[]");
        var result = await Source(handler).FetchAsync("dQw4w9WgXcQ");

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("empty");
    }
}
