using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Services.Config;

namespace VideoCortex.Core.Services.Llm;

/// <summary>
/// <see cref="IVideoSummarizer"/> backed by any OpenAI-compatible chat-completions endpoint. A
/// single call enforces structured output via <c>response_format = json_schema</c> so the result
/// is always a parseable <see cref="VideoSummary"/>. Unlike a triage card, the summary is meant
/// to be a reasonably complete account of the video, since Phase 6's report is synthesized from it.
/// </summary>
/// <remarks>
/// Model and endpoint both come from <see cref="LlmSettings"/> (Video Cortex keeps the model with
/// the endpoint config, so no separate model knob lives in <c>SummarySettings</c>). Read via
/// <see cref="IOptionsMonitor{TOptions}"/> per call so a Settings edit takes effect without restart.
/// </remarks>
public sealed class OpenAiSummarizer(
    IOptionsMonitor<LlmSettings> llm,
    OpenAiClient client,
    ILogger<OpenAiSummarizer> logger) : IVideoSummarizer
{
    private const string SystemPrompt =
        "You write a faithful, self-contained summary of a single YouTube video from its " +
        "transcript. This is NOT a short triage blurb — it will be combined with other video " +
        "summaries into a synthesized report, so capture the video's substance: its main claims, " +
        "key points, notable examples, and conclusions, organized clearly.\n\n" +
        "Return JSON matching the supplied schema:\n" +
        "- title: a clean, descriptive title for the video (you may refine a clickbait title into " +
        "something accurate).\n" +
        "- description: one sentence (<= 200 chars) capturing what the video is about.\n" +
        "- tags: 3-8 short lowercase topic tags.\n" +
        "- body_markdown: the full summary as Markdown (headings, lists, etc. as helpful). Do NOT " +
        "include a top-level H1 title — start with prose or an H2 section; the page supplies the H1.";

    private static readonly JsonNode FormatSchema = JsonNode.Parse("""
        {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "title":         { "type": "string" },
            "description":   { "type": "string" },
            "tags":          { "type": "array", "items": { "type": "string" } },
            "body_markdown": { "type": "string" }
          },
          "required": ["title", "description", "tags", "body_markdown"]
        }
        """)!;

    public async Task<VideoSummary> SummarizeAsync(
        string title, string channel, string transcript, string? aiInstructions = null, CancellationToken ct = default)
    {
        var settings = llm.CurrentValue;

        var user = new StringBuilder();
        user.AppendLine($"Video title: {title}");
        if (!string.IsNullOrWhiteSpace(channel))
            user.AppendLine($"Channel: {channel}");
        if (!string.IsNullOrWhiteSpace(aiInstructions))
        {
            user.AppendLine();
            user.AppendLine("Project-specific instructions (follow these where relevant):");
            user.AppendLine(aiInstructions);
        }
        user.AppendLine();
        user.AppendLine("Transcript ([mm:ss] prefixed):");
        user.Append(TranscriptWindow.Trim(transcript));

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
                JsonSchema = new JsonSchemaSpec { Name = "video_summary", Strict = true, Schema = FormatSchema },
            },
        };

        var resp = await client.PostChatAsync(settings, request, ct);
        var raw = resp.Choices.FirstOrDefault()?.Message?.Content?.Trim() ?? string.Empty;

        try
        {
            var parsed = JsonSerializer.Deserialize<VideoSummary>(raw);
            if (parsed is null)
                throw new SummaryParseException("Summarizer returned null JSON.");
            if (string.IsNullOrWhiteSpace(parsed.Title) || string.IsNullOrWhiteSpace(parsed.BodyMarkdown))
                throw new SummaryParseException("Summarizer returned empty title or body_markdown.");
            return parsed with { Tags = parsed.Tags ?? Array.Empty<string>() };
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Summarizer response was not parseable: {Raw}", raw);
            throw new SummaryParseException($"Summarizer response was not parseable: {ex.Message}", ex);
        }
    }
}
