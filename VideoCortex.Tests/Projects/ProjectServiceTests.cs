using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Db;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Library;
using VideoCortex.Features.Projects.Models;
using VideoCortex.Features.Projects.Services;

namespace VideoCortex.Tests.Projects;

public class ProjectServiceTests : IDisposable
{
    private readonly SqliteInMemoryFixture _fx = new();
    private readonly string _root = TestPaths.NewTempDir();

    public void Dispose()
    {
        _fx.Dispose();
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    private ProjectService NewService(VideoCortexDbContext db)
    {
        var store = new OkfLibraryStore(_root, TestPaths.OkfTemplatesDir());
        var options = Options.Create(new LibrarySettings { RootPath = _root });
        return new ProjectService(db, store, options, NullLogger<ProjectService>.Instance);
    }

    private static ProjectFormModel Form(string name, string? desc = null)
        => new() { Name = name, Description = desc };

    [Fact]
    public async Task CreateAsync_Persists_Row_And_Scaffolds_Library()
    {
        using var db = _fx.CreateContext();
        var result = await NewService(db).CreateAsync(Form("Local LLM Inference", "notes"));

        result.Success.Should().BeTrue();
        result.Project!.Slug.Should().Be("local-llm-inference");

        // Row persisted.
        using var verify = _fx.CreateContext();
        verify.Projects.Should().ContainSingle().Which.Name.Should().Be("Local LLM Inference");

        // Library scaffolded on disk.
        var indexPath = Path.Combine(_root, "Local LLM Inference", "index.html");
        File.Exists(indexPath).Should().BeTrue();
        File.ReadAllText(indexPath).Should().Contain("<h1>Local LLM Inference</h1>");
    }

    [Fact]
    public async Task CreateAsync_Duplicate_Name_Returns_IsDuplicate()
    {
        using var db = _fx.CreateContext();
        var svc = NewService(db);

        (await svc.CreateAsync(Form("Local LLM"))).Success.Should().BeTrue();
        var second = await svc.CreateAsync(Form("Local LLM"));

        second.Success.Should().BeFalse();
        second.IsDuplicate.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_Distinct_Names_Colliding_Slugs_Get_Unique_Slugs()
    {
        using var db = _fx.CreateContext();
        var svc = NewService(db);

        var first = await svc.CreateAsync(Form("A B"));
        var second = await svc.CreateAsync(Form("A-B")); // different name, same base slug "a-b"

        first.Project!.Slug.Should().Be("a-b");
        second.Project!.Slug.Should().Be("a-b-2");
    }

    [Fact]
    public async Task DeleteAsync_RowsOnly_Leaves_Folder_On_Disk()
    {
        using var db = _fx.CreateContext();
        var svc = NewService(db);
        var created = await svc.CreateAsync(Form("Keep Me"));
        var id = created.Project!.Id;
        var dir = Path.Combine(_root, "Keep Me");
        Directory.Exists(dir).Should().BeTrue();

        var del = await svc.DeleteAsync(id, deleteLibraryFolder: false);

        del.Success.Should().BeTrue();
        using var verify = _fx.CreateContext();
        verify.Projects.Should().BeEmpty();
        Directory.Exists(dir).Should().BeTrue("rows-only delete must preserve the precious library");
    }

    [Fact]
    public async Task DeleteAsync_WithFolder_Removes_Folder()
    {
        using var db = _fx.CreateContext();
        var svc = NewService(db);
        var created = await svc.CreateAsync(Form("Drop Me"));
        var id = created.Project!.Id;
        var dir = Path.Combine(_root, "Drop Me");

        var del = await svc.DeleteAsync(id, deleteLibraryFolder: true);

        del.Success.Should().BeTrue();
        Directory.Exists(dir).Should().BeFalse();
    }

    [Fact]
    public async Task GetBySlugAsync_Returns_Detail_With_FolderName()
    {
        using var db = _fx.CreateContext();
        var svc = NewService(db);
        await svc.CreateAsync(Form("Research Notes", "d"));

        var detail = await svc.GetBySlugAsync("research-notes");

        detail.Should().NotBeNull();
        detail!.LibraryFolderName.Should().Be("Research Notes");
        detail.Description.Should().Be("d");
    }
}
