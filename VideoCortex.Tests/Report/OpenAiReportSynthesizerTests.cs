using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Llm;
using VideoCortex.Tests.Services.Transcripts;

namespace VideoCortex.Tests.Report;

public class OpenAiReportSynthesizerTests
{
    private static string Completion(string content)
        => JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content } } } });

    private static OpenAiReportSynthesizer Synthesizer(FakeHttpMessageHandler handler, string apiKey = "sk-test")
    {
        var client = new OpenAiClient(handler);
        var llm = new TestOptionsMonitor<LlmSettings>(new LlmSettings { Model = "gpt-4o-mini", ApiKey = apiKey });
        return new OpenAiReportSynthesizer(llm, client, NullLogger<OpenAiReportSynthesizer>.Instance);
    }

    private static ReportSynthesisContext Ctx() => new(
        "AI Notes", null,
        new[]
        {
            new VideoSummaryInput(1, "aaaaaaaaaaa", "Vid A", "Chan", "vid-a", "A", "about a", "body a"),
        });

    [Fact]
    public async Task Parses_Items_And_Sends_Strict_Json_Schema()
    {
        var payload = JsonSerializer.Serialize(new
        {
            library_description = "A project about AI.",
            items = new[]
            {
                new { title = "Topic One", body_markdown = "Detail about one.", source_slugs = new[] { "vid-a" } },
                new { title = "Topic Two", body_markdown = "Detail about two.", source_slugs = new[] { "vid-a", "vid-b" } },
            },
        });
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, Completion(payload));

        var result = await Synthesizer(handler).SynthesizeAsync(Ctx());

        result.LibraryDescription.Should().Be("A project about AI.");
        result.Items.Should().HaveCount(2);
        result.Items[0].Title.Should().Be("Topic One");
        result.Items[1].SourceSlugs.Should().BeEquivalentTo(new[] { "vid-a", "vid-b" });

        // The request enforced strict structured output.
        handler.LastRequestBody.Should().Contain("json_schema").And.Contain("\"strict\":true").And.Contain("source_slugs");
    }

    [Fact]
    public async Task Drops_Items_Missing_Title_Or_Body()
    {
        var payload = JsonSerializer.Serialize(new
        {
            library_description = "d",
            items = new[]
            {
                new { title = "Kept", body_markdown = "has body", source_slugs = new[] { "vid-a" } },
                new { title = "", body_markdown = "no title", source_slugs = new string[0] },
                new { title = "no body", body_markdown = "", source_slugs = new string[0] },
            },
        });
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, Completion(payload));

        var result = await Synthesizer(handler).SynthesizeAsync(Ctx());

        result.Items.Should().ContainSingle().Which.Title.Should().Be("Kept");
    }

    [Fact]
    public async Task Non2xx_Throws_HttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "server boom");
        var act = () => Synthesizer(handler).SynthesizeAsync(Ctx());
        (await act.Should().ThrowAsync<HttpRequestException>()).And.Message.Should().Contain("server boom");
    }

    [Fact]
    public async Task Malformed_Content_Throws_ReportSynthesisException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, Completion("not json"));
        var act = () => Synthesizer(handler).SynthesizeAsync(Ctx());
        await act.Should().ThrowAsync<ReportSynthesisException>();
    }

    [Fact]
    public async Task Empty_Items_Throws_ReportSynthesisException()
    {
        var payload = JsonSerializer.Serialize(new { library_description = "d", items = Array.Empty<object>() });
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, Completion(payload));
        var act = () => Synthesizer(handler).SynthesizeAsync(Ctx());
        await act.Should().ThrowAsync<ReportSynthesisException>();
    }
}
