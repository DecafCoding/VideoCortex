namespace VideoCortex.Core.Services.Triage;

/// <summary>Result of a per-video retry.</summary>
public sealed record RetryResult(bool Ok, string? Message)
{
    public static RetryResult Success() => new(true, null);
    public static RetryResult Fail(string message) => new(false, message);
}

/// <summary>Clears a video's park/error state and re-queues it at the correct pending stage.</summary>
public interface IVideoRetryCommand
{
    Task<RetryResult> RetryVideoAsync(int videoId, CancellationToken ct = default);
}
