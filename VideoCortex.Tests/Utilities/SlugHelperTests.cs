using System.Text.RegularExpressions;
using FluentAssertions;
using VideoCortex.Core.Services.Utilities;

namespace VideoCortex.Tests.Utilities;

public class SlugHelperTests
{
    [Theory]
    [InlineData("Local LLM Inference", "local-llm-inference")]
    [InlineData("  Hello,  World!  ", "hello-world")]
    [InlineData("A/B", "a-b")]
    [InlineData("Café — Déjà", "cafe-deja")] // diacritics transliterated to ASCII base letters
    public void ToSlug_Normalizes(string input, string expected)
        => SlugHelper.ToSlug(input, "fb").Should().Be(expected);

    [Theory]
    [InlineData("Local LLM Inference")]
    [InlineData("Café — Déjà")]
    [InlineData("日本語 mixed 42")]
    public void ToSlug_Always_Matches_SafePattern(string input)
        => Regex.IsMatch(SlugHelper.ToSlug(input, "fb"), "^[A-Za-z0-9._-]+$").Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("  ...  ")]
    [InlineData("!!!")]
    public void ToSlug_Empty_Returns_Fallback(string input)
        => SlugHelper.ToSlug(input, "fb").Should().Be("fb");

    [Fact]
    public async Task UniqueSlugAsync_Increments_Past_Taken()
    {
        var taken = new HashSet<string> { "x", "x-2" };
        var result = await SlugHelper.UniqueSlugAsync("x", s => Task.FromResult(taken.Contains(s)), "fb");
        result.Should().Be("x-3");
    }

    [Fact]
    public async Task UniqueSlugAsync_Returns_Preferred_When_Free()
    {
        var result = await SlugHelper.UniqueSlugAsync("free", _ => Task.FromResult(false), "fb");
        result.Should().Be("free");
    }
}
