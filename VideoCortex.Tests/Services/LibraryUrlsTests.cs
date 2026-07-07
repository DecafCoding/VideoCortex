using FluentAssertions;
using VideoCortex.Services;

namespace VideoCortex.Tests.Services;

/// <summary>Covers URL building for hosted OKF libraries, including folder names with spaces.</summary>
public class LibraryUrlsTests
{
    [Fact]
    public void ForProject_Simple_Name_Builds_Rooted_Url()
    {
        LibraryUrls.ForProject("Gardening").Should().Be("/library/Gardening/");
    }

    [Fact]
    public void ForProject_Name_With_Spaces_Is_Escaped()
    {
        LibraryUrls.ForProject("Wild Flowers").Should().Be("/library/Wild%20Flowers/");
    }

    [Fact]
    public void ForConcept_Appends_Escaped_Slug_With_Html_Extension()
    {
        LibraryUrls.ForConcept("Wild Flowers", "prairie-restoration")
            .Should().Be("/library/Wild%20Flowers/prairie-restoration.html");
    }
}
