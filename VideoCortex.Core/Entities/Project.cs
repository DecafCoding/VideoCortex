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

    public DateTime CreatedAt { get; set; }

    public ICollection<Video> Videos { get; set; } = new List<Video>();
}
