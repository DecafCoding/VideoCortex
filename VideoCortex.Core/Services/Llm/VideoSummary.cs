using System.Text.Json.Serialization;

namespace VideoCortex.Core.Services.Llm;

/// <summary>
/// Structured output of <see cref="IVideoSummarizer"/>. JSON names mirror the strict json_schema
/// exactly — any drift breaks strict-mode validation / deserialization.
/// </summary>
public sealed record VideoSummary(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("body_markdown")] string BodyMarkdown);
