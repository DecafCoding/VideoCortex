using FluentAssertions;
using Microsoft.Extensions.Configuration;
using VideoCortex.Core.Services.Config;
using VideoCortex.Features.Settings.Models;
using VideoCortex.Features.Settings.Services;

namespace VideoCortex.Tests.Settings;

public class SettingsServiceTests : IDisposable
{
    private readonly string _dir = TestPaths.NewTempDir();
    private readonly string _overlayPath;
    private readonly OverlayWriter _overlay;

    public SettingsServiceTests()
    {
        _overlayPath = Path.Combine(_dir, "appsettings.Local.json");
        _overlay = new OverlayWriter(_overlayPath);
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
    }

    private SettingsService Service(params (string, string?)[] config)
    {
        var cfg = new ConfigurationBuilder()
            .AddInMemoryCollection(config.ToDictionary(k => k.Item1, v => v.Item2))
            .Build();
        return new SettingsService(cfg, _overlay);
    }

    private static SettingsForm Valid() => new()
    {
        Model = "gpt-4o-mini",
        BaseUrl = "",
        LibraryRootPath = OperatingSystem.IsWindows() ? @"C:\Temp\SecondBrain" : "/tmp/SecondBrain",
    };

    [Fact]
    public async Task Load_Reports_Secrets_As_Set_Without_Echoing()
    {
        var form = await Service(("Llm:ApiKey", "sk-secret"), ("Apify:Token", "")).LoadAsync();
        form.ApiKeyIsSet.Should().BeTrue();
        form.ApifyTokenIsSet.Should().BeFalse();
    }

    [Fact]
    public async Task Save_Rejects_NonAbsolute_BaseUrl()
    {
        var form = Valid();
        form.BaseUrl = "not-a-url";
        (await Service().SaveAsync(form)).Ok.Should().BeFalse();
    }

    [Fact]
    public async Task Save_Rejects_Relative_Library_Root()
    {
        var form = Valid();
        form.LibraryRootPath = "relative/path";
        (await Service().SaveAsync(form)).Ok.Should().BeFalse();
    }

    [Fact]
    public async Task Save_Rejects_Zero_Poll_Interval()
    {
        var form = Valid();
        form.SummaryIdlePollSeconds = 0;
        (await Service().SaveAsync(form)).Ok.Should().BeFalse();
    }

    [Fact]
    public async Task Save_Persists_Expected_Keys()
    {
        var form = Valid();
        form.Model = "llama3";
        form.BaseUrl = "http://localhost:11434/v1";

        var result = await Service().SaveAsync(form);

        result.Ok.Should().BeTrue();
        (await _overlay.GetAsync("Llm:Model")).Should().Be("llama3");
        (await _overlay.GetAsync("Llm:BaseUrl")).Should().Be("http://localhost:11434/v1");
        (await _overlay.GetAsync("Report:CoalesceDebounceSeconds")).Should().Be("10");
    }

    [Fact]
    public async Task Save_Never_Persists_Secrets_To_Overlay()
    {
        // Secrets come from user-secrets (dev) or the environment (prod); saving the
        // Settings form must not write them to the overlay even when they exist in config.
        var result = await Service(("Llm:ApiKey", "sk-secret"), ("Apify:Token", "apify_tok")).SaveAsync(Valid());

        result.Ok.Should().BeTrue();
        (await _overlay.GetAsync("Llm:ApiKey")).Should().BeNull();
        (await _overlay.GetAsync("Apify:Token")).Should().BeNull();
    }
}
