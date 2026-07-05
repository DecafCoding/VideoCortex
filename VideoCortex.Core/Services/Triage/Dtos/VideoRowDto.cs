using VideoCortex.Core.Entities;

namespace VideoCortex.Core.Services.Triage.Dtos;

/// <summary>A video row for the project detail table. <c>Title</c> is null until Phase 4 backfills it.</summary>
public record VideoRowDto(int Id, string YoutubeVideoId, string? Title, VideoStatus Status, DateTime AddedAt);
