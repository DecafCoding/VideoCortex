using FluentAssertions;
using Microsoft.Extensions.Configuration;
using VideoCortex.Core.Services.Config;

namespace VideoCortex.Tests.Config;

public class OverlayWriterTests : IDisposable
{
    private readonly string _dir = TestPaths.NewTempDir();
    private readonly string _path;

    public OverlayWriterTests() => _path = Path.Combine(_dir, "appsettings.Local.json");

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private OverlayWriter Writer() => new(_path);

    [Fact]
    public async Task Set_Then_Get_RoundTrips()
    {
        var w = Writer();
        await w.SetAsync("Llm:BaseUrl", "http://localhost:11434/v1");
        (await w.GetAsync("Llm:BaseUrl")).Should().Be("http://localhost:11434/v1");
    }

    [Fact]
    public async Task Merge_Preserves_Unrelated_Keys()
    {
        var w = Writer();
        await w.SetAsync("Apify:Token", "tok");
        await w.SetAsync("Llm:Model", "llama3");

        (await w.GetAsync("Apify:Token")).Should().Be("tok");
        (await w.GetAsync("Llm:Model")).Should().Be("llama3");
    }

    [Fact]
    public async Task Set_Empty_Removes_The_Leaf()
    {
        var w = Writer();
        await w.SetAsync("Llm:ApiKey", "sk-secret");
        await w.SetAsync("Llm:ApiKey", "");

        (await w.GetAsync("Llm:ApiKey")).Should().BeNull();
        // The key is absent from the file (not written as "").
        File.ReadAllText(_path).Should().NotContain("ApiKey");
    }

    [Fact]
    public async Task No_Tmp_File_Left_After_Write()
    {
        await Writer().SetAsync("Llm:Model", "gpt-4o-mini");
        File.Exists(_path + ".tmp").Should().BeFalse();
    }

    [Fact]
    public async Task Missing_File_Is_Created_On_First_Write()
    {
        File.Exists(_path).Should().BeFalse();
        await Writer().SetAsync("Llm:Model", "m");
        File.Exists(_path).Should().BeTrue();
    }

    [Fact]
    public async Task Written_Overlay_Binds_Through_Configuration()
    {
        var w = Writer();
        await w.SetAsync("Llm:Model", "bound-model");
        await w.SetAsync("Llm:BaseUrl", "http://x/v1");

        var config = new ConfigurationBuilder().AddJsonFile(_path, optional: false, reloadOnChange: false).Build();
        var settings = config.GetSection(LlmSettings.Section).Get<LlmSettings>()!;
        settings.Model.Should().Be("bound-model");
        settings.BaseUrl.Should().Be("http://x/v1");
    }
}
