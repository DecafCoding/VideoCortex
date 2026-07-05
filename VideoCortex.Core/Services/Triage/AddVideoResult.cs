namespace VideoCortex.Core.Services.Triage;

/// <summary>Outcome of an add-video-by-URL attempt. Transient (never persisted).</summary>
public enum AddVideoOutcome
{
    Added,
    DuplicateInProject,
    InvalidUrl,
}

/// <summary>Result of <c>IVideoCommands.AddVideoByUrlAsync</c>.</summary>
public record AddVideoResult(AddVideoOutcome Outcome, int? VideoId, string? YoutubeVideoId, string? Message)
{
    public bool Success => Outcome == AddVideoOutcome.Added;

    public static AddVideoResult Added(int videoId, string youtubeId)
        => new(AddVideoOutcome.Added, videoId, youtubeId, null);

    public static AddVideoResult Duplicate(string youtubeId)
        => new(AddVideoOutcome.DuplicateInProject, null, youtubeId,
            "That video is already in this project.");

    public static AddVideoResult Invalid(string message)
        => new(AddVideoOutcome.InvalidUrl, null, null, message);
}
