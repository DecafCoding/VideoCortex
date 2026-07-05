using VideoCortex.Core.Services.Triage.Dtos;

namespace VideoCortex.Core.Services.Triage;

/// <summary>Read operations feeding the project detail video table.</summary>
public interface IVideoQueries
{
    /// <summary>A project's videos, newest-added first.</summary>
    Task<IReadOnlyList<VideoRowDto>> ListForProjectAsync(int projectId, CancellationToken ct = default);
}
