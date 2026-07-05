namespace VideoCortex.Tests;

/// <summary>Locates repo assets (the bundled OKF templates) from the test output directory.</summary>
internal static class TestPaths
{
    /// <summary>Absolute path to the bundled <c>VideoCortex/wwwroot/okf</c> templates directory.</summary>
    public static string OkfTemplatesDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VideoCortex.slnx")))
            dir = dir.Parent;
        if (dir is null)
            throw new InvalidOperationException("Could not locate repo root (VideoCortex.slnx) from test output.");
        return Path.Combine(dir.FullName, "VideoCortex", "wwwroot", "okf");
    }

    /// <summary>A fresh, empty temp directory for a test; caller deletes it.</summary>
    public static string NewTempDir()
    {
        var path = Path.Combine(Path.GetTempPath(), "vc-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
