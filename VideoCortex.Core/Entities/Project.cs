namespace VideoCortex.Core.Entities;

/// <summary>
/// A research topic the user creates. Each project is a standalone OKF-HTML library
/// on disk (its own folder under the library root) plus its videos in the database.
/// </summary>
public class Project
{
    public int Id { get; set; }

    /// <summary>Display name; also the on-disk library folder name (sanitized).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Unique, filesystem-safe identifier used for routes and uniqueness.</summary>
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Per-project guidance threaded into the summary and report LLM prompts.</summary>
    public string? AIInstructions { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Idle;

    /// <summary>UTC time the project's report (index.html) was last (re)generated.</summary>
    public DateTime? ReportUpdatedAt { get; set; }

    /// <summary>
    /// Set (via <c>??=</c>) when a video reaches Summarized, on "Rebuild report", or on
    /// "remove video"; cleared only after a successful regeneration. Non-null = needs a report pass.
    /// </summary>
    public DateTime? ReportDirtySince { get; set; }

    /// <summary>Consecutive failed report regenerations; parks the project past the retry cap.</summary>
    public int ReportRetryCount { get; set; }

    /// <summary>Earliest UTC time the report worker may retry after a failure (exponential backoff).</summary>
    public DateTime? ReportNextAttemptAt { get; set; }

    /// <summary>Message from the most recent failed report regeneration; cleared on success.</summary>
    public string? LastReportError { get; set; }

    public DateTime CreatedAt { get; set; }

    public ICollection<Video> Videos { get; set; } = new List<Video>();
}
