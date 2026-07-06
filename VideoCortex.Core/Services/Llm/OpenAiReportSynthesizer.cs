using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Services.Config;

namespace VideoCortex.Core.Services.Llm;

/// <summary>
/// Synthesizes a project's report via an OpenAI-compatible endpoint (reusing the Phase-5
/// <see cref="OpenAiClient"/> transport) with strict structured output. The model returns
/// <b>inner body HTML only</b> for <c>report_html</c> — the OKF root-index skeleton, <c>okf-meta</c>,
/// and <c>&lt;h1&gt;</c> are supplied deterministically by <c>OkfLibraryStore.WriteReportAsync</c>.
/// </summary>
public sealed class OpenAiReportSynthesizer(
    IOptionsMonitor<LlmSettings> llm,
    OpenAiClient client,
    ILogger<OpenAiReportSynthesizer> logger) : IReportSynthesizer
{
    private const string SystemPrompt =
        "You write a synthesized, multi-section report covering a set of YouTube videos that a " +
        "user has collected into one research project. You are given each video's compact summary " +
        "(not its transcript). Produce a briefing that reads as a coherent whole across the videos.\n\n" +
        "Return JSON matching the schema:\n" +
        "- library_description: one plain-text sentence (<= 200 chars) describing the project as a whole.\n" +
        "- report_html: INNER BODY HTML ONLY. Do NOT emit <!doctype>, <html>, <head>, <body>, an " +
        "okf-meta block, or a top-level <h1> — those are added by the page wrapper. Organize the " +
        "report into thematic sections, each introduced by an <h2>. Where a claim comes from a " +
        "specific video, cite it inline with a RELATIVE link to that video's concept page: " +
        "<a href=\"CONCEPT_SLUG.html\">…</a> (use the exact concept slug given for each video; never " +
        "an absolute or http link). END with a <section> whose first child is <h2>Sources</h2> " +
        "containing a <ul> with one <li> per video: a relative link to its concept page plus a " +
        "one-line description.";

    private static readonly JsonNode FormatSchema = JsonNode.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "library_description": { "type": "string" },
            "report_html":         { "type": "string" }
          },
          "required": ["library_description", "report_html"]
        }
        """)!;

    public async Task<ReportSynthesisResult> SynthesizeAsync(ReportSynthesisContext ctx, CancellationToken ct = default)
    {
        var settings = llm.CurrentValue;

        var user = new StringBuilder();
        user.AppendLine($"Project: {ctx.ProjectName}");
        if (!string.IsNullOrWhiteSpace(ctx.AIInstructions))
        {
            user.AppendLine();
            user.AppendLine("Project-specific instructions (follow where relevant):");
            user.AppendLine(ctx.AIInstructions);
        }
        user.AppendLine();
        user.AppendLine($"Videos ({ctx.Videos.Count}):");
        foreach (var v in ctx.Videos)
        {
            user.AppendLine();
            user.AppendLine($"### {v.SummaryTitle ?? v.Title}");
            user.AppendLine($"concept_slug: {v.ConceptSlug}   (cite as \"{v.ConceptSlug}.html\")");
            if (!string.IsNullOrWhiteSpace(v.ChannelTitle)) user.AppendLine($"channel: {v.ChannelTitle}");
            if (!string.IsNullOrWhiteSpace(v.SummaryDescription)) user.AppendLine($"description: {v.SummaryDescription}");
            if (!string.IsNullOrWhiteSpace(v.SummaryBodyMd)) user.AppendLine($"summary:\n{v.SummaryBodyMd}");
        }

        var request = new ChatCompletionRequest
        {
            Model = settings.Model,
            Messages =
            [
                new ChatMessage { Role = "system", Content = SystemPrompt },
                new ChatMessage { Role = "user", Content = user.ToString() },
            ],
            ResponseFormat = new ResponseFormat
            {
                Type = "json_schema",
                JsonSchema = new JsonSchemaSpec { Name = "report", Strict = true, Schema = FormatSchema },
            },
        };

        var resp = await client.PostChatAsync(settings, request, ct);
        var raw = resp.Choices.FirstOrDefault()?.Message?.Content?.Trim() ?? string.Empty;

        try
        {
            var parsed = JsonSerializer.Deserialize<ReportPayload>(raw);
            if (parsed is null || string.IsNullOrWhiteSpace(parsed.ReportHtml))
                throw new ReportSynthesisException("Synthesizer returned empty report_html.");
            return new ReportSynthesisResult(parsed.LibraryDescription ?? string.Empty, parsed.ReportHtml);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Report synthesis response was not parseable: {Raw}", raw);
            throw new ReportSynthesisException($"Report synthesis response was not parseable: {ex.Message}", ex);
        }
    }

    private sealed record ReportPayload(
        [property: JsonPropertyName("library_description")] string? LibraryDescription,
        [property: JsonPropertyName("report_html")] string? ReportHtml);
}
