using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Library;
using VideoCortex.Features.Projects.Models;
using VideoCortex.Features.Projects.Services;

namespace VideoCortex.Tests.Projects;

/// <summary>
/// End-to-end proof of the Phase 2 slice through the service layer: create → conformant
/// on-disk OKF library → list → get-by-slug → rows-only delete (folder survives) →
/// delete-with-folder. Uses a real <see cref="OkfLibraryStore"/> against a temp root and the
/// in-memory SQLite fixture — no SignalR circuit, so it is deterministic regression coverage.
/// </summary>
public class ProjectFlowSmokeTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fx = new();
    private readonly string _root = TestPaths.NewTempDir();

    public void Dispose()
    {
        _fx.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task Full_Create_List_Get_Delete_Cycle()
    {
        using var db = _fx.CreateContext();
        var store = new OkfLibraryStore(_root, TestPaths.OkfTemplatesDir());
        var svc = new ProjectService(
            db, store, Options.Create(new LibrarySettings { RootPath = _root }),
            NullLogger<ProjectService>.Instance);

        // 1) Create → conformant library on disk.
        var created = await svc.CreateAsync(new ProjectFormModel { Name = "Edge AI", Description = "notes" });
        created.Success.Should().BeTrue();

        var dir = Path.Combine(_root, "Edge AI");
        var indexPath = Path.Combine(dir, "index.html");
        File.Exists(Path.Combine(dir, "theme.css")).Should().BeTrue();
        File.Exists(indexPath).Should().BeTrue();

        var html = File.ReadAllText(indexPath);
        html.Should().Contain("href=\"theme.css\"").And.NotContain("href=\"/theme.css\"");
        html.Should().NotContain("okf-index-group").And.NotContain("{{");
        var metaMatch = Regex.Match(html, "id=\"okf-meta\"[^>]*>(.*?)</script>", RegexOptions.Singleline);
        var meta = JsonDocument.Parse(metaMatch.Groups[1].Value.Trim()).RootElement;
        meta.GetProperty("type").GetString().Should().Be("Index");
        meta.GetProperty("okf_html_version").GetString().Should().Be("0.1");

        // 2) List → appears.
        var list = await svc.ListAsync();
        list.Should().ContainSingle().Which.Slug.Should().Be("edge-ai");

        // 3) Get-by-slug → detail with folder name.
        var detail = await svc.GetBySlugAsync("edge-ai");
        detail.Should().NotBeNull();
        detail!.LibraryFolderName.Should().Be("Edge AI");

        // 4) Rows-only delete → row gone, folder preserved.
        (await svc.DeleteAsync(detail.Id, deleteLibraryFolder: false)).Success.Should().BeTrue();
        (await svc.ListAsync()).Should().BeEmpty();
        Directory.Exists(dir).Should().BeTrue("the precious OKF library must survive a rows-only delete");

        // 5) Recreate and delete WITH folder → folder gone.
        var again = await svc.CreateAsync(new ProjectFormModel { Name = "Edge AI" });
        Directory.Exists(dir).Should().BeTrue();
        (await svc.DeleteAsync(again.Project!.Id, deleteLibraryFolder: true)).Success.Should().BeTrue();
        Directory.Exists(dir).Should().BeFalse();
    }
}
