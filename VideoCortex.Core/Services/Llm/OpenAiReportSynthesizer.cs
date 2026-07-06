using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Services.Config;

namespace VideoCortex.Core.Services.Llm;

/// <summary>
/// Aggregates a project's report via an OpenAI-compatible endpoint (reusing the Phase-5
/// <see cref="OpenAiClient"/> transport) with strict structured output. The model returns a
/// structured, deduplicated list of <c>items</c> (never HTML) — the OKF root-index skeleton,
/// <c>okf-meta</c>, <c>&lt;h1&gt;</c>, and the per-item HTML (heading, body, "Sources" line) are all
/// rendered deterministically by <c>OkfLibraryStore.WriteReportAsync</c>.
/// </summary>
public sealed class OpenAiReportSynthesizer(
    IOptionsMonitor<LlmSettings> llm,
    OpenAiClient client,
    ILogger<OpenAiReportSynthesizer> logger) : IReportSynthesizer
{
    private const string SystemPrompt =
        "You build a project's INDEX: a single cumulative, deduplicated list of atomic items " +
        "aggregated across a set of YouTube videos the user collected. You are given each video's " +
        "compact summary (not its transcript).\n\n" +
        "The project-specific instructions define what one \"item\" is and what information to " +
        "capture for it (e.g. one item per topic, tool, recipe, argument, …). TREAT THOSE " +
        "INSTRUCTIONS AS YOUR PRIMARY DIRECTIVE — they determine the shape of the list. If no " +
        "instructions are given, treat each distinct key topic or takeaway as one item.\n\n" +
        "Rules for the list:\n" +
        "- BE EXHAUSTIVE. Include every distinct item from every video. Do NOT thematically group, " +
        "compress, summarize away, or drop items. If the videos collectively cover 30 distinct " +
        "items, return about 30 items — this is an aggregation, not a summary of a summary.\n" +
        "- MERGE DUPLICATES. When two or more videos cover the SAME item, output ONE item that " +
        "combines the information from all of them, and list the concept slug of every source video " +
        "in source_slugs. Items that are merely similar (not the same) stay separate.\n" +
        "- Order items logically (related items near each other is fine), but every item stands alone.\n\n" +
        "Return JSON matching the schema:\n" +
        "- library_description: one plain-text sentence (<= 200 chars) describing the project as a whole.\n" +
        "- items: the list. For each item:\n" +
        "  - title: a concise name for the item.\n" +
        "  - body_markdown: the useful information about the item, as Markdown (lists/emphasis as " +
        "helpful). Merge details across sources when the item was duplicated. Do NOT repeat the " +
        "title as a heading and do NOT include source links here — those are added by the wrapper.\n" +
        "  - source_slugs: the concept slug of EVERY video this item came from (one or more), each " +
        "exactly as given for that video (no \".html\", no path, no http link).";

    private static readonly JsonNode FormatSchema = JsonNode.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "library_description": { "type": "string" },
            "items": {
              "type": "array",
              "items": {
                "type": "object",
                "additionalProperties": false,
                "properties": {
                  "title":         { "type": "string" },
                  "body_markdown": { "type": "string" },
                  "source_slugs":  { "type": "array", "items": { "type": "string" } }
                },
                "required": ["title", "body_markdown", "source_slugs"]
              }
            }
          },
          "required": ["library_description", "items"]
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
            var items = (parsed?.Items ?? new List<ReportItemPayload>())
                .Where(i => !string.IsNullOrWhiteSpace(i.Title) && !string.IsNullOrWhiteSpace(i.BodyMarkdown))
                .Select(i => new ReportItem(
                    i.Title!.Trim(),
                    i.BodyMarkdown!,
                    (IReadOnlyList<string>)(i.SourceSlugs ?? new List<string>())))
                .ToList();
            if (items.Count == 0)
                throw new ReportSynthesisException("Synthesizer returned no usable items.");
            return new ReportSynthesisResult(parsed!.LibraryDescription ?? string.Empty, items);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Report synthesis response was not parseable: {Raw}", raw);
            throw new ReportSynthesisException($"Report synthesis response was not parseable: {ex.Message}", ex);
        }
    }

    private sealed record ReportPayload(
        [property: JsonPropertyName("library_description")] string? LibraryDescription,
        [property: JsonPropertyName("items")] List<ReportItemPayload>? Items);

    private sealed record ReportItemPayload(
        [property: JsonPropertyName("title")] string? Title,
        [property: JsonPropertyName("body_markdown")] string? BodyMarkdown,
        [property: JsonPropertyName("source_slugs")] List<string>? SourceSlugs);
}
