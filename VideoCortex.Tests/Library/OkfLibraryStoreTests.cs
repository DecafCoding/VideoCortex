using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Library;

namespace VideoCortex.Tests.Library;

public class OkfLibraryStoreTests : IDisposable
{
    private readonly string _root = TestPaths.NewTempDir();
    private readonly string _templates = TestPaths.OkfTemplatesDir();

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private OkfLibraryStore Store() => new(_root, _templates);

    [Fact]
    public async Task CreateLibrary_Writes_Conformant_Empty_Index()
    {
        await Store().CreateLibraryAsync(new Project { Name = "Test Lib", Description = "d" });

        var dir = Path.Combine(_root, "Test Lib");
        var themePath = Path.Combine(dir, "theme.css");
        var indexPath = Path.Combine(dir, "index.html");

        File.Exists(themePath).Should().BeTrue();
        File.Exists(indexPath).Should().BeTrue();

        // theme.css is byte-for-byte identical to the bundled template.
        File.ReadAllBytes(themePath).Should().Equal(File.ReadAllBytes(Path.Combine(_templates, "theme.css")));

        var html = File.ReadAllText(indexPath);
        html.Should().Contain("id=\"okf-meta\"");
        html.Should().Contain("href=\"theme.css\"");
        html.Should().NotContain("href=\"/theme.css\"");
        html.Should().Contain("<title>Test Lib</title>");
        html.Should().Contain("<h1>Test Lib</h1>");
        html.Should().NotContain("okf-index-group");
        html.Should().NotContain("{{"); // no unreplaced placeholders

        // okf-meta parses as JSON with the required conformance fields.
        var meta = ParseOkfMeta(html);
        meta.GetProperty("type").GetString().Should().Be("Index");
        meta.GetProperty("okf_html_version").GetString().Should().Be("0.1");
        meta.GetProperty("title").GetString().Should().Be("Test Lib");
    }

    [Fact]
    public async Task CreateLibrary_Is_Idempotent()
    {
        var store = Store();
        var project = new Project { Name = "Repeat", Description = "one" };

        await store.CreateLibraryAsync(project);
        project.Description = "two";
        var act = () => store.CreateLibraryAsync(project);

        await act.Should().NotThrowAsync();
        File.ReadAllText(Path.Combine(_root, "Repeat", "index.html")).Should().Contain("two");
    }

    [Fact]
    public async Task CreateLibrary_JsonEncodes_Meta_Title_With_Special_Chars()
    {
        // '&' and accented chars are valid Windows folder names but must be correctly encoded
        // so the okf-meta JSON still parses and round-trips.
        await Store().CreateLibraryAsync(new Project { Name = "Café & Co", Description = "x" });

        var html = File.ReadAllText(Path.Combine(_root, "Café & Co", "index.html"));
        var meta = ParseOkfMeta(html);
        meta.GetProperty("title").GetString().Should().Be("Café & Co");
    }

    [Fact]
    public async Task CreateLibrary_Never_Touches_Sibling_Library()
    {
        // Seed a pre-existing hand-built sibling library with sentinel contents.
        var sibling = Path.Combine(_root, "Wild Flowers");
        Directory.CreateDirectory(sibling);
        var siblingIndex = Path.Combine(sibling, "index.html");
        var siblingTheme = Path.Combine(sibling, "theme.css");
        File.WriteAllText(siblingIndex, "SENTINEL-INDEX");
        File.WriteAllText(siblingTheme, "SENTINEL-THEME");

        await Store().CreateLibraryAsync(new Project { Name = "Other", Description = "d" });

        // The sibling is byte-for-byte untouched.
        File.ReadAllText(siblingIndex).Should().Be("SENTINEL-INDEX");
        File.ReadAllText(siblingTheme).Should().Be("SENTINEL-THEME");

        // Only the sibling and the new project's folder exist under the root.
        Directory.GetDirectories(_root).Select(Path.GetFileName)
            .Should().BeEquivalentTo("Wild Flowers", "Other");
    }

    [Fact]
    public async Task CreateLibrary_By_Same_Name_Writes_Into_That_Folder()
    {
        // Folder-name collisions are ProjectService's responsibility to prevent (unique Name);
        // the store itself is idempotent-by-name and only ever rewrites its own two files.
        var existing = Path.Combine(_root, "Shared Name");
        Directory.CreateDirectory(existing);
        File.WriteAllText(Path.Combine(existing, "keep.txt"), "USER-DATA");

        await Store().CreateLibraryAsync(new Project { Name = "Shared Name", Description = "d" });

        // The store wrote its two files but left unrelated files in the folder untouched.
        File.Exists(Path.Combine(existing, "index.html")).Should().BeTrue();
        File.Exists(Path.Combine(existing, "theme.css")).Should().BeTrue();
        File.ReadAllText(Path.Combine(existing, "keep.txt")).Should().Be("USER-DATA");
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("a/b")]
    [InlineData("a\\b")]
    [InlineData("..")]
    [InlineData("   ")]
    public async Task CreateLibrary_Rejects_Unsafe_Names(string name)
    {
        var act = () => Store().CreateLibraryAsync(new Project { Name = name });
        await act.Should().ThrowAsync<ArgumentException>();
    }

    private static JsonElement ParseOkfMeta(string html)
    {
        var m = Regex.Match(html, "id=\"okf-meta\"[^>]*>(.*?)</script>", RegexOptions.Singleline);
        m.Success.Should().BeTrue("index.html must contain an okf-meta script block");
        return JsonDocument.Parse(m.Groups[1].Value.Trim()).RootElement;
    }
}
