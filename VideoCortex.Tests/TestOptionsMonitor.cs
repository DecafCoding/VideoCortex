using Microsoft.Extensions.Options;

namespace VideoCortex.Tests;

/// <summary>Minimal <see cref="IOptionsMonitor{T}"/> returning a fixed value for tests.</summary>
internal sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue { get; } = value;
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
