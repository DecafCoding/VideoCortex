using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using VideoCortex.Core.Services.Config;
using VideoCortex.Core.Services.Llm;

namespace VideoCortex.Tests.Config;

public class HotReloadTests
{
    [Fact]
    public async Task Overlay_Edit_Is_Reflected_In_OptionsMonitor_Without_Restart()
    {
        var dir = TestPaths.NewTempDir();
        var path = Path.Combine(dir, "appsettings.Local.json");
        File.WriteAllText(path, "{}");
        try
        {
            var config = new ConfigurationBuilder()
                .AddJsonFile(path, optional: true, reloadOnChange: true)
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.Configure<LlmSettings>(config.GetSection(LlmSettings.Section));
            var provider = services.BuildServiceProvider();
            var monitor = provider.GetRequiredService<IOptionsMonitor<LlmSettings>>();

            await new OverlayWriter(path).SetAsync("Llm:BaseUrl", "http://reloaded/v1");

            // reloadOnChange fires on a debounce — poll briefly rather than asserting synchronously.
            var reflected = await WaitForAsync(
                () => monitor.CurrentValue.BaseUrl == "http://reloaded/v1", TimeSpan.FromSeconds(5));

            reflected.Should().BeTrue("an overlay edit must reach IOptionsMonitor without a restart");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void OpenAiClientCache_Rebuilds_Client_When_BaseUrl_Changes()
    {
        using var cache = new OpenAiClientCache();

        var a = cache.Get("http://a/", "key", 30);
        cache.Get("http://a/", "key", 30).Should().BeSameAs(a, "same endpoint reuses the cached client");

        var b = cache.Get("http://b/", "key", 30);
        b.Should().NotBeSameAs(a, "a base-URL change rebuilds the client — this is what makes hot reload live");
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return true;
            await Task.Delay(100);
        }
        return condition();
    }
}
