namespace VideoCortex.Core.Services.Triage;

/// <summary>Write operations on videos within a project.</summary>
public interface IVideoCommands
{
    /// <summary>
    /// Parses <paramref name="input"/> (a YouTube URL or bare 11-char ID) and, if valid and
    /// not already in the project, inserts a <c>Video</c> at <c>Status = Added</c>. Metadata
    /// (title, channel, duration) is left null for Phase 4 to backfill. No fetch happens here.
    /// </summary>
    Task<AddVideoResult> AddVideoByUrlAsync(int projectId, string input, CancellationToken ct = default);
}
