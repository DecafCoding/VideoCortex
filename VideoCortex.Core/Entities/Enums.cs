namespace VideoCortex.Core.Entities;

/// <summary>
/// Lifecycle state of a <see cref="Project"/>'s on-disk report.
/// Persisted as an int by declaration order — do not reorder.
/// </summary>
public enum ProjectStatus
{
    Idle = 0,
    Updating = 1,
    Error = 2,
}

/// <summary>
/// Pipeline stage of a <see cref="Video"/>. Persisted as an int by declaration
/// order — do not reorder. Happy path: Added → Transcribed → Summarized → Published.
/// </summary>
public enum VideoStatus
{
    Added = 0,
    Transcribed = 1,
    Summarized = 2,
    Published = 3,
    NoTranscript = 4,
    Error = 5,
}
