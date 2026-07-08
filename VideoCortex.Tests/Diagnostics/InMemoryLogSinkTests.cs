using FluentAssertions;
using Microsoft.Extensions.Logging;
using VideoCortex.Services.Logging;

namespace VideoCortex.Tests.Diagnostics;

/// <summary>Ring-buffer and level-filter behavior of the Diagnostics page's log sink.</summary>
public class InMemoryLogSinkTests
{
    [Fact]
    public void Snapshot_MixedLevels_FiltersByMinLevelNewestFirst()
    {
        var sink = new InMemoryLogSink();
        var logger = new InMemoryLoggerProvider(sink).CreateLogger("Test.Category");

        logger.LogDebug("debug dropped");
        logger.LogInformation("info one");
        logger.LogWarning("warn one");
        logger.LogError(new InvalidOperationException("boom"), "error one");

        sink.Snapshot(LogLevel.Information).Select(e => e.Message)
            .Should().Equal("error one", "warn one", "info one");
        sink.Snapshot(LogLevel.Error).Should().ContainSingle()
            .Which.Exception.Should().Contain("boom");
    }

    [Fact]
    public void Add_BeyondCapacity_DropsOldestEntries()
    {
        var sink = new InMemoryLogSink();
        for (var i = 0; i < InMemoryLogSink.Capacity + 10; i++)
            sink.Add(new LogEntry(DateTime.UtcNow, LogLevel.Information, "Cat", $"msg {i}", null));

        var snapshot = sink.Snapshot();
        snapshot.Should().HaveCount(InMemoryLogSink.Capacity);
        snapshot[0].Message.Should().Be($"msg {InMemoryLogSink.Capacity + 9}");
        snapshot[^1].Message.Should().Be("msg 10");
    }
}
