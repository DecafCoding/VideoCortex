namespace VideoCortex.Core.Entities;

/// <summary>
/// A YouTube video the user has added to a <see cref="Project"/>. Belongs to exactly one
/// project (no sharing) and moves through the pipeline stages in <see cref="VideoStatus"/>.
/// </summary>
public class Video
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    /// <summary>Canonical 11-character YouTube video ID.</summary>
    public string YoutubeVideoId { get; set; } = string.Empty;

    // --- Metadata (backfilled from Apify in Phase 4) ---
    public string? Title { get; set; }
    public string? ChannelTitle { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Description { get; set; }
    public int? DurationSeconds { get; set; }
    public long? ViewCount { get; set; }
    public long? LikeCount { get; set; }
    public long? CommentsCount { get; set; }

    // --- Pipeline state ---
    public VideoStatus Status { get; set; } = VideoStatus.Added;
    public bool Parked { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LastError { get; set; }

    // --- Transcript (Phase 4) ---
    public string? TranscriptText { get; set; }
    public string? TranscriptLang { get; set; }

    // --- Summary (Phase 5) ---
    public string? SummaryTitle { get; set; }
    public string? SummaryDescription { get; set; }
    public string? SummaryBodyMd { get; set; }

    /// <summary>Filesystem-safe slug for this video's concept page (&lt;slug&gt;.html).</summary>
    public string? ConceptSlug { get; set; }

    // --- Timestamps (UTC) ---
    public DateTime AddedAt { get; set; }
    public DateTime? TranscribedAt { get; set; }
    public DateTime? SummarizedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}
