using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Library;

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

    private const string ReportBody =
        "<h2>Theme A</h2><p>Point with a citation <a href=\"vid-a.html\">Vid A</a>.</p>" +
        "<section><h2>Sources</h2><ul><li><a href=\"vid-a.html\">Vid A</a> — about A</li></ul></section>";

    [Fact]
    public async Task WriteReport_Produces_Conformant_Index()
    {
        // '&' is valid in a Windows folder name; '<'/'>' are not, so the angle brackets that
        // exercise HTML-escaping go in the description (a parameter), not the folder name.
        var project = new Project { Name = "R & D Notes", Slug = "r-d-notes", CreatedAt = DateTime.UtcNow };
        await _store.CreateLibraryAsync(project);
        await _store.WriteReportAsync(project, "About <R&D> & more.", ReportBody);

        var html = File.ReadAllText(Path.Combine(_root, "R & D Notes", "index.html"));

        html.Should().NotContain("{{");
        html.Should().Contain("href=\"theme.css\"").And.NotContain("href=\"/theme.css\"");
        html.Should().Contain("<h2>Theme A</h2>");                       // thematic section
        html.Should().Contain("<h2>Sources</h2>");                        // Sources section
        html.Should().Contain("href=\"vid-a.html\"");                     // relative concept link
        html.Should().NotContain("href=\"/vid-a.html\"");

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
        await _store.WriteReportAsync(project, "first", "<h2>One</h2>");
        await _store.WriteReportAsync(project, "second", "<h2>Two</h2>");

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
