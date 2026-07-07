namespace VideoCortex.Services;

/// <summary>
/// Builds the app-relative URLs under which the on-disk OKF libraries are served
/// (see the static-file mapping in <c>Program.cs</c>). Folder names may contain spaces
/// (e.g. <c>Wild Flowers</c>), so every path segment is URL-escaped.
/// </summary>
public static class LibraryUrls
{
    /// <summary>Request path prefix the library root is mounted at.</summary>
    public const string RequestPath = "/library";

    /// <summary>URL of a project library's root report (its <c>index.html</c>).</summary>
    public static string ForProject(string libraryFolderName)
        => $"{RequestPath}/{Uri.EscapeDataString(libraryFolderName)}/";

    /// <summary>URL of a single concept page within a project library.</summary>
    public static string ForConcept(string libraryFolderName, string conceptSlug)
        => ForProject(libraryFolderName) + Uri.EscapeDataString(conceptSlug + ".html");
}
