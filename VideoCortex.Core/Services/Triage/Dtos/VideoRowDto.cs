using VideoCortex.Core.Entities;

namespace VideoCortex.Core.Services.Triage.Dtos;

/// <summary>A video row for the project detail table. <c>Title</c> is null until Phase 4 backfills it;
/// <c>ConceptSlug</c> is null until the concept page is written (Phase 5).</summary>
public record VideoRowDto(
    int Id, string YoutubeVideoId, string? Title, VideoStatus Status, DateTime AddedAt,
    string? ConceptSlug, bool Parked, string? LastError);
