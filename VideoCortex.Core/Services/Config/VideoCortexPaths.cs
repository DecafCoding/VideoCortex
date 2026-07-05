namespace VideoCortex.Core.Services.Config;

/// <summary>
/// Resolves the well-known local paths Video Cortex uses. The state DB and config overlay
/// live under <c>%USERPROFILE%\.videocortex</c>; the durable OKF-HTML libraries default to
/// <c>Documents\SecondBrain</c>. Never hardcode a user profile path — always resolve here.
/// </summary>
public static class VideoCortexPaths
{
    /// <summary>Per-user data directory: <c>%USERPROFILE%\.videocortex</c>.</summary>
    public static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".videocortex");

    /// <summary>SQLite state database path.</summary>
    public static string DbPath => Path.Combine(DataDir, "app.db");

    /// <summary>Writable, hot-reloaded config overlay path.</summary>
    public static string OverlayPath => Path.Combine(DataDir, "appsettings.Local.json");

    /// <summary>Default root for the on-disk OKF-HTML libraries: <c>Documents\SecondBrain</c>.</summary>
    public static string DefaultLibraryRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "SecondBrain");

    /// <summary>Ensures the data directory exists. Idempotent.</summary>
    public static void EnsureDataDir() => Directory.CreateDirectory(DataDir);
}
