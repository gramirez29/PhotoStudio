using PhotoStudio.Application.Storage;

namespace PhotoStudio.Application.UnitTests.Storage;

/// <summary>
/// Tests of <see cref="ObjectKey"/>: the last line of defense for the keys of the objects in the storage.
/// </summary>
public sealed class ObjectKeyTests
{
    /// <summary>
    /// The keys the backend builds from random identifiers are accepted.
    /// </summary>
    /// <param name="key">The key.</param>
    [Theory]
    [InlineData("galleries/0197a000-0000-7000-8000-000000000001/proofs/9f2c1b.jpg")]
    [InlineData("a")]
    [InlineData("a/b_c-d.e/f")]
    [InlineData("a..b.jpg")]
    public void IsValid_AcceptsPlainKeys(string key) => ObjectKey.IsValid(key).ShouldBeTrue();

    /// <summary>
    /// Anything that could climb out of a prefix, hide an empty segment or confuse the storage or a log line is refused.
    /// </summary>
    /// <param name="key">The key.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/a")]
    [InlineData("a/")]
    [InlineData("a//b")]
    [InlineData("a/./b")]
    [InlineData("a/../b")]
    [InlineData("..")]
    [InlineData("a b")]
    [InlineData("a?x=1")]
    [InlineData("a#b")]
    [InlineData("a%2e%2e/b")]
    [InlineData("a\\b")]
    [InlineData("a\nb")]
    [InlineData("a\u0000b")]
    [InlineData("carpeta/ñandú.jpg")]
    public void IsValid_RefusesWhatCouldBeDangerous(string? key) => ObjectKey.IsValid(key).ShouldBeFalse();

    /// <summary>
    /// A key is as long as it can be, and not one character more.
    /// </summary>
    [Fact]
    public void IsValid_HasALengthLimit()
    {
        ObjectKey.IsValid(new string('a', ObjectKey.MaxLength)).ShouldBeTrue();
        ObjectKey.IsValid(new string('a', ObjectKey.MaxLength + 1)).ShouldBeFalse();
    }

    /// <summary>
    /// The check that throws names the argument and never repeats the key.
    /// </summary>
    [Fact]
    public void EnsureValid_ThrowsWithoutEchoingTheKey()
    {
        var error = Should.Throw<ArgumentException>(() => ObjectKey.EnsureValid("../secret", "key"));

        error.ParamName.ShouldBe("key");
        error.Message.ShouldNotContain("secret");
        Should.NotThrow(() => ObjectKey.EnsureValid("fine/key.jpg", "key"));
    }
}
