using VideoCortex.Core.Entities;

namespace VideoCortex.Core.Services.Library;

/// <summary>
/// Writes each project's standalone OKF-HTML library to disk under
/// <c>&lt;libraryRoot&gt;/&lt;Project.Name&gt;/</c>. All writes are atomic (temp file +
/// <see cref="File.Move(string, string, bool)"/> with overwrite) and UTF-8 without BOM, so a
/// reader never observes a half-written file. The store only ever touches a project's own
/// folder — it never enumerates the library root or modifies sibling libraries.
/// </summary>
/// <remarks>
/// This interface grows across phases; only <see cref="CreateLibraryAsync"/> exists today.
/// Planned future members:
/// <list type="bullet">
///   <item><description><c>WriteConceptPageAsync(Project, Video, summary)</c> — Phase 5: renders a per-video concept page (<c>&lt;video-slug&gt;.html</c>).</description></item>
///   <item><description><c>WriteReportAsync(Project, description, reportHtml)</c> — Phase 6: (re)generates the synthesized root <c>index.html</c> report.</description></item>
///   <item><description><c>DeleteConceptAsync(Project, Video)</c> — Phase 6: removes a concept page when a video is removed.</description></item>
///   <item><description><c>DeleteLibraryAsync(Project)</c> — opt-in library folder deletion.</description></item>
/// </list>
/// </remarks>
public interface IOkfLibraryStore
{
    /// <summary>
    /// Scaffolds the project's OKF library folder: creates <c>&lt;libraryRoot&gt;/&lt;Name&gt;/</c>,
    /// copies the shared <c>theme.css</c> into it byte-for-byte, and writes an OKF-conformant
    /// empty root <c>index.html</c> (valid <c>okf-meta</c> with <c>type: "Index"</c> and
    /// <c>okf_html_version: "0.1"</c>, a relative <c>theme.css</c> link, no index groups).
    /// Idempotent: re-running overwrites the <c>index.html</c> shell and re-copies
    /// <c>theme.css</c> without throwing.
    /// </summary>
    Task CreateLibraryAsync(Project project, CancellationToken ct = default);
}
