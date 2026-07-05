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
    public async Task Parses_Result_And_Sends_Strict_Json_Schema()
    {
        var payload = JsonSerializer.Serialize(new
        {
            library_description = "A project about AI.",
            report_html = "<h2>Theme</h2><p><a href=\"vid-a.html\">A</a></p><section><h2>Sources</h2><ul><li><a href=\"vid-a.html\">Vid A</a></li></ul></section>",
        });
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, Completion(payload));

        var result = await Synthesizer(handler).SynthesizeAsync(Ctx());

        result.LibraryDescription.Should().Be("A project about AI.");
        result.ReportHtml.Should().Contain("<h2>Theme</h2>").And.Contain("<h2>Sources</h2>");

        // The request enforced strict structured output.
        handler.LastRequestBody.Should().Contain("json_schema").And.Contain("\"strict\":true").And.Contain("report_html");
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
    public async Task Empty_ReportHtml_Throws_ReportSynthesisException()
    {
        var payload = JsonSerializer.Serialize(new { library_description = "d", report_html = "" });
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, Completion(payload));
        var act = () => Synthesizer(handler).SynthesizeAsync(Ctx());
        await act.Should().ThrowAsync<ReportSynthesisException>();
    }
}
