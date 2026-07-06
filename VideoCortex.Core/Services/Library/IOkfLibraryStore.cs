using VideoCortex.Core.Entities;
using VideoCortex.Core.Services.Llm;

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

    /// <summary>
    /// Renders a video's summary into an OKF concept page (<c>&lt;concept-slug&gt;.html</c>) inside
    /// the project's library folder. The LLM supplies only Markdown for the body; the page shell,
    /// <c>okf-meta</c>, and <c>&lt;h1&gt;</c> are filled deterministically from the bundled template.
    /// Returns the concept slug used (stable if <see cref="Video.ConceptSlug"/> is already set,
    /// otherwise derived from the title with project-scoped collision resolution).
    /// </summary>
    Task<string> WriteConceptPageAsync(Project project, Video video, VideoSummary summary, CancellationToken ct = default);

    /// <summary>
    /// (Re)writes the project's root <c>index.html</c> as the aggregated report: renders
    /// <paramref name="items"/> into a running list (one <c>&lt;h2&gt;</c> + Markdig body + a
    /// per-item "Sources" line linking each cited concept page) and wraps it in the OKF root-index
    /// template (<c>okf-meta</c> <c>type: "Index"</c>, <c>okf_html_version: "0.1"</c>, relative
    /// <c>theme.css</c>). <paramref name="sources"/> maps each concept slug to a display title for
    /// the source links; item slugs not present in it are dropped (no broken/foreign links). Written
    /// atomically, so a good prior file is never left half-overwritten.
    /// </summary>
    Task WriteReportAsync(
        Project project, string libraryDescription,
        IReadOnlyList<ReportItem> items, IReadOnlyList<ReportSource> sources, CancellationToken ct = default);

    /// <summary>
    /// Deletes a video's concept page (<c>&lt;ConceptSlug&gt;.html</c>) from the project folder.
    /// No-op when the slug is null/empty or the file is absent. Only ever touches the project's
    /// own folder.
    /// </summary>
    Task DeleteConceptPageAsync(Project project, Video video, CancellationToken ct = default);
}

/// <summary>
/// A concept page a report item may cite: its <paramref name="Slug"/> (the on-disk file stem) and a
/// human <paramref name="Title"/> used as the link text in the report's per-item "Sources" line.
/// </summary>
public sealed record ReportSource(string Slug, string Title);
