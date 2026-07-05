using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Llm;
using VideoCortex.Tests.Services.Transcripts;

namespace VideoCortex.Tests.Llm;

public class OpenAiSummarizerTests
{
    private static string Completion(string content)
        => JsonSerializer.Serialize(new { choices = new[] { new { message = new { role = "assistant", content } } } });

    private static OpenAiSummarizer Summarizer(FakeHttpMessageHandler handler, string apiKey = "sk-test")
    {
        var client = new OpenAiClient(handler); // internal test-seam ctor
        var llm = new TestOptionsMonitor<LlmSettings>(new LlmSettings { Model = "gpt-4o-mini", ApiKey = apiKey });
        return new OpenAiSummarizer(llm, client, NullLogger<OpenAiSummarizer>.Instance);
    }

    [Fact]
    public async Task Parses_Canned_Structured_Completion()
    {
        var summaryJson = JsonSerializer.Serialize(new
        {
            title = "Refined Title",
            description = "What it is about.",
            tags = new[] { "ai", "local" },
            body_markdown = "## Overview\n\nThe video explains things.",
        });
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, Completion(summaryJson));

        var result = await Summarizer(handler).SummarizeAsync("Clickbait", "Chan", "[00:00] hello");

        result.Title.Should().Be("Refined Title");
        result.Description.Should().Be("What it is about.");
        result.Tags.Should().BeEquivalentTo("ai", "local");
        result.BodyMarkdown.Should().Contain("## Overview");
        handler.LastRequestUri!.ToString().Should().Contain("/v1/chat/completions");
    }

    [Fact]
    public async Task Non2xx_Throws_HttpRequestException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "boom");
        var act = () => Summarizer(handler).SummarizeAsync("t", "c", "x");
        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Malformed_Content_Throws_SummaryParseException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, Completion("this is not json"));
        var act = () => Summarizer(handler).SummarizeAsync("t", "c", "x");
        await act.Should().ThrowAsync<SummaryParseException>();
    }

    [Fact]
    public async Task Empty_Body_Field_Throws_SummaryParseException()
    {
        var summaryJson = JsonSerializer.Serialize(new
        {
            title = "T", description = "D", tags = Array.Empty<string>(), body_markdown = "",
        });
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, Completion(summaryJson));
        var act = () => Summarizer(handler).SummarizeAsync("t", "c", "x");
        await act.Should().ThrowAsync<SummaryParseException>();
    }

    [Fact]
    public async Task Blank_ApiKey_Throws_InvalidOperationException()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, Completion("{}"));
        var act = () => Summarizer(handler, apiKey: "").SummarizeAsync("t", "c", "x");
        await act.Should().ThrowAsync<InvalidOperationException>();
        handler.CallCount.Should().Be(0); // failed before any HTTP call
    }
}
