using FluentAssertions;
using VideoCortex.Services;

namespace VideoCortex.Tests.Services;

public class DesktopFileOpenerTests
{
    // The success path launches the OS default handler (a browser), so it is not automated here;
    // this covers the guard that prevents launching anything for a missing/blank path.
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void TryOpen_Blank_Path_Fails_Cleanly(string path)
    {
        var ok = new DesktopFileOpener().TryOpen(path, out var error);
        ok.Should().BeFalse();
        error.Should().NotBeNull();
    }

    [Fact]
    public void TryOpen_Missing_File_Fails_Cleanly()
    {
        var ok = new DesktopFileOpener().TryOpen(
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".html"), out var error);
        ok.Should().BeFalse();
        error.Should().Contain("not found");
    }
}
