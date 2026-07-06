using System.Diagnostics;

namespace VideoCortex.Services;

/// <summary>
/// Opens a local file with the OS default handler (e.g. the default browser for <c>.html</c>).
/// Video Cortex is a single-user local app, so the Blazor circuit runs on the same machine as
/// the browser — this is how "Open Library" / "open page" actually reveal an on-disk OKF file,
/// since a browser refuses to navigate to a <c>file://</c> link from an <c>http://</c> page.
/// </summary>
public interface IDesktopFileOpener
{
    bool TryOpen(string absolutePath, out string? error);
}

public sealed class DesktopFileOpener : IDesktopFileOpener
{
    public bool TryOpen(string absolutePath, out string? error)
    {
        error = null;
        try
        {
            if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
            {
                error = $"File not found: {absolutePath}";
                return false;
            }
            // UseShellExecute lets the OS pick the default program (browser for HTML).
            Process.Start(new ProcessStartInfo { FileName = absolutePath, UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
