using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Library;
using VideoCortex.Core.Services.Llm;

namespace VideoCortex.Tests.Report;

public class ReportWriterTests : IDisposable
{
    private readonly string _root = TestPaths.NewTempDir();
    private readonly OkfLibraryStore _store;

    public ReportWriterTests() => _store = new OkfLibraryStore(_root, TestPaths.OkfTemplatesDir());

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private static readonly List<ReportItem> ReportItems =
    [
        new("Theme A", "Point with detail about A.", ["vid-a"]),
        new("Theme B", "Point covered by two videos.", ["vid-a", "vid-b"]),
    ];

    private static readonly List<ReportSource> Sources =
    [
        new("vid-a", "Vid A"),
        new("vid-b", "Vid B"),
    ];

    [Fact]
    public async Task WriteReport_Produces_Conformant_Index()
    {
        // '&' is valid in a Windows folder name; '<'/'>' are not, so the angle brackets that
        // exercise HTML-escaping go in the description (a parameter), not the folder name.
        var project = new Project { Name = "R & D Notes", Slug = "r-d-notes", CreatedAt = DateTime.UtcNow };
        await _store.CreateLibraryAsync(project);
        await _store.WriteReportAsync(project, "About <R&D> & more.", ReportItems, Sources);

        var html = File.ReadAllText(Path.Combine(_root, "R & D Notes", "index.html"));

        html.Should().NotContain("{{");
        html.Should().Contain("href=\"theme.css\"").And.NotContain("href=\"/theme.css\"");
        html.Should().Contain("<h2>Theme A</h2>").And.Contain("<h2>Theme B</h2>"); // one heading per item
        html.Should().Contain("<strong>Sources:</strong>");                        // per-item sources line
        html.Should().Contain("href=\"vid-a.html\"").And.Contain(">Vid A</a>");     // relative link w/ title text
        html.Should().NotContain("href=\"/vid-a.html\"");
        // The item covered by two videos cites both.
        html.Should().Contain("href=\"vid-b.html\"");

        // Scalar placeholders are escaped in HTML context (not raw).
        html.Should().Contain("<h1>R &amp; D Notes</h1>");
        html.Should().Contain("About &lt;R&amp;D&gt; &amp; more.").And.NotContain("About <R&D> & more.");

        // okf-meta parses and is a conformant root index.
        var m = Regex.Match(html, "id=\"okf-meta\"[^>]*>(.*?)</script>", RegexOptions.Singleline);
        var meta = JsonDocument.Parse(m.Groups[1].Value.Trim()).RootElement;
        meta.GetProperty("type").GetString().Should().Be("Index");
        meta.GetProperty("okf_html_version").GetString().Should().Be("0.1");
        meta.GetProperty("title").GetString().Should().Be("R & D Notes");
    }

    [Fact]
    public async Task WriteReport_Failure_Path_Never_Reached_Here_But_Atomic_Overwrites()
    {
        var project = new Project { Name = "Proj", Slug = "proj", CreatedAt = DateTime.UtcNow };
        await _store.CreateLibraryAsync(project);
        await _store.WriteReportAsync(project, "first", [new("One", "body one", [])], []);
        await _store.WriteReportAsync(project, "second", [new("Two", "body two", [])], []);

        var html = File.ReadAllText(Path.Combine(_root, "Proj", "index.html"));
        html.Should().Contain("<h2>Two</h2>").And.NotContain("<h2>One</h2>");
    }

    [Fact]
    public async Task DeleteConceptPage_Removes_When_Present_And_NoOps_When_Absent()
    {
        var project = new Project { Name = "Proj", Slug = "proj", CreatedAt = DateTime.UtcNow };
        await _store.CreateLibraryAsync(project);
        var dir = Path.Combine(_root, "Proj");
        File.WriteAllText(Path.Combine(dir, "some-video.html"), "x");

        await _store.DeleteConceptPageAsync(project, new Video { ConceptSlug = "some-video" });
        File.Exists(Path.Combine(dir, "some-video.html")).Should().BeFalse();

        // No-op when absent or slug null.
        await _store.DeleteConceptPageAsync(project, new Video { ConceptSlug = "some-video" });
        await _store.DeleteConceptPageAsync(project, new Video { ConceptSlug = null });
    }

    [Fact]
    public async Task DeleteConceptPage_Refuses_Path_Escaping_Slug()
    {
        var project = new Project { Name = "Proj", Slug = "proj", CreatedAt = DateTime.UtcNow };
        await _store.CreateLibraryAsync(project);

        var act = () => _store.DeleteConceptPageAsync(project, new Video { ConceptSlug = "../evil" });
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
