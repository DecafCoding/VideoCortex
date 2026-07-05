using FluentAssertions;
using VideoCortex.Core.Services.Transcripts;

namespace VideoCortex.Tests.Services.Transcripts;

public class SrtConverterTests
{
    [Fact]
    public void MultiCue_Converts_To_MmSs_Lines()
    {
        const string srt = "1\n00:00:00,000 --> 00:00:02,000\nhello\n\n2\n00:00:02,500 --> 00:00:04,000\nworld\n";

        var result = SrtConverter.ConvertSrtToPrdFormat(srt);

        result.Should().Contain("[00:00] hello");
        result.Should().Contain("[00:02] world");
    }

    [Fact]
    public void Hours_Field_Is_Folded_Into_Minutes()
    {
        const string srt = "1\n01:05:03,000 --> 01:05:06,000\nlate line\n";

        SrtConverter.ConvertSrtToPrdFormat(srt).Should().Contain("[65:03] late line");
    }

    [Fact]
    public void Multiline_Caption_Is_Joined()
    {
        const string srt = "1\n00:00:01,000 --> 00:00:03,000\nline one\nline two\n";

        SrtConverter.ConvertSrtToPrdFormat(srt).Should().Contain("[00:01] line one line two");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not an srt file")]
    public void Malformed_Or_Blank_Returns_Empty(string input)
        => SrtConverter.ConvertSrtToPrdFormat(input).Should().BeEmpty();
}

public class DurationParserTests
{
    [Theory]
    [InlineData("PT4M13S", 253)]
    [InlineData("PT1H2M3S", 3723)]
    [InlineData("253", 253)]          // bare seconds
    [InlineData("4:13", 253)]         // mm:ss
    [InlineData("1:05:03", 3903)]     // hh:mm:ss
    public void Parses_Known_Formats(string input, int expected)
        => DurationParser.ParseToSeconds(input).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    public void Unrecognized_Returns_Zero(string? input)
        => DurationParser.ParseToSeconds(input).Should().Be(0);
}
