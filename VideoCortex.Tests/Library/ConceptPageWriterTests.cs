using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Library;
using VideoCortex.Core.Services.Llm;

namespace VideoCortex.Tests.Library;

public class ConceptPageWriterTests : IDisposable
{
    private readonly string _root = TestPaths.NewTempDir();
    private readonly OkfLibraryStore _store;
    private readonly Project _project = new() { Name = "AI Notes", Slug = "ai-notes", CreatedAt = DateTime.UtcNow };

    public ConceptPageWriterTests()
    {
        _store = new OkfLibraryStore(_root, TestPaths.OkfTemplatesDir());
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private static VideoSummary Summary(string title, params string[] tags)
        => new(title, "A one-line description.", tags,
            "## Overview\n\nSome prose.\n\n- point one\n- point two\n\n```csharp\nvar x = 1;\n```");

    private static Video Video(string id) => new() { YoutubeVideoId = id, Status = VideoStatus.Transcribed };

    [Fact]
    public async Task Writes_Conformant_Concept_Page()
    {
        await _store.CreateLibraryAsync(_project);
        var slug = await _store.WriteConceptPageAsync(_project, Video("dQw4w9WgXcQ"), Summary("Great Video", "ai", "local"));

        var path = Path.Combine(_root, "AI Notes", slug + ".html");
        File.Exists(path).Should().BeTrue();

        var html = File.ReadAllText(path);
        html.Should().NotContain("{{");                       // no leftover placeholders
        html.Should().Contain("href=\"theme.css\"").And.NotContain("href=\"/theme.css\"");
        html.Should().Contain("<h1>Great Video</h1>");
        html.Should().Contain("<h2 id=\"overview\">Overview</h2>").And.Contain("<code");  // Markdig rendered body
        html.Should().Contain("<span class=\"okf-tag\">ai</span>");

        var meta = ParseOkfMeta(html);
        meta.GetProperty("type").GetString().Should().Be("Video");
        meta.GetProperty("title").GetString().Should().Be("Great Video");
        meta.GetProperty("resource").GetString().Should().Be("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        meta.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).Should().BeEquivalentTo("ai", "local");
    }

    [Fact]
    public async Task Unsafe_Title_Yields_Safe_Slug()
    {
        await _store.CreateLibraryAsync(_project);
        var slug = await _store.WriteConceptPageAsync(_project, Video("abc12345678"), Summary("A/B: Test? <weird>"));

        Regex.IsMatch(slug, "^[A-Za-z0-9._-]+$").Should().BeTrue();
        File.Exists(Path.Combine(_root, "AI Notes", slug + ".html")).Should().BeTrue();
    }

    [Fact]
    public async Task Duplicate_Titles_Get_Distinct_Filenames()
    {
        await _store.CreateLibraryAsync(_project);
        var slug1 = await _store.WriteConceptPageAsync(_project, Video("aaaaaaaaaaa"), Summary("Same Title"));
        var slug2 = await _store.WriteConceptPageAsync(_project, Video("bbbbbbbbbbb"), Summary("Same Title"));

        slug1.Should().NotBe(slug2);
        File.Exists(Path.Combine(_root, "AI Notes", slug1 + ".html")).Should().BeTrue();
        File.Exists(Path.Combine(_root, "AI Notes", slug2 + ".html")).Should().BeTrue();
    }

    [Fact]
    public async Task Special_Chars_In_Title_Keep_Meta_Json_Valid()
    {
        await _store.CreateLibraryAsync(_project);
        var slug = await _store.WriteConceptPageAsync(_project, Video("ccccccccccc"), Summary("Quotes \" & <tags>"));

        var html = File.ReadAllText(Path.Combine(_root, "AI Notes", slug + ".html"));
        ParseOkfMeta(html).GetProperty("title").GetString().Should().Be("Quotes \" & <tags>");
    }

    private static JsonElement ParseOkfMeta(string html)
    {
        var m = Regex.Match(html, "id=\"okf-meta\"[^>]*>(.*?)</script>", RegexOptions.Singleline);
        m.Success.Should().BeTrue();
        return JsonDocument.Parse(m.Groups[1].Value.Trim()).RootElement;
    }
}
