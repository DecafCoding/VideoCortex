using FluentAssertions;
using VideoCortex.Core.Services.Triage;

namespace VideoCortex.Tests.Core.Triage;

public class YouTubeInputParserTests
{
    private const string Id = "dQw4w9WgXcQ";

    [Theory]
    [InlineData("dQw4w9WgXcQ")]                                                  // bare id
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("http://youtube.com/watch?v=dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=42s&list=PLxxxx")] // extra params
    [InlineData("https://youtu.be/dQw4w9WgXcQ")]
    [InlineData("https://youtu.be/dQw4w9WgXcQ?si=abc123")]                        // share suffix
    [InlineData("https://www.youtube.com/shorts/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/embed/dQw4w9WgXcQ")]
    [InlineData("https://www.youtube.com/live/dQw4w9WgXcQ")]
    [InlineData("youtu.be/dQw4w9WgXcQ")]                                          // no scheme
    [InlineData("  https://youtu.be/dQw4w9WgXcQ  ")]                              // trimmed
    [InlineData("https://www.youtube.com/watch?v=dQw4w9WgXcQextrastuff")]         // over-long → first 11
    public void Parse_Valid_Returns_CanonicalId(string input)
    {
        var result = YouTubeInputParser.Parse(input);
        result.IsValid.Should().BeTrue();
        result.VideoId.Should().Be(Id);
    }

    [Fact]
    public void Parse_Preserves_Case_And_Symbols_In_Id()
    {
        var result = YouTubeInputParser.Parse("aB_9-cD3xyz");
        result.IsValid.Should().BeTrue();
        result.VideoId.Should().Be("aB_9-cD3xyz");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("https://vimeo.com/12345")]
    [InlineData("https://www.youtube.com/watch?v=short")]                // too-short id
    [InlineData("waytoolongvalue123")]                                   // bare over-long → Invalid
    [InlineData("https://www.youtube.com/")]
    [InlineData("@handle")]
    [InlineData("dQw4w9WgXc")]                                           // 10 chars, bare, too short
    public void Parse_Invalid_Returns_Invalid(string? input)
    {
        var result = YouTubeInputParser.Parse(input);
        result.IsValid.Should().BeFalse();
        result.VideoId.Should().BeNull();
    }
}
