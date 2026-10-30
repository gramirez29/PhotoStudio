using PhotoStudio.Application.Pagination;

namespace PhotoStudio.Application.UnitTests.Pagination;

/// <summary>
/// Tests of <see cref="PageCursor"/> and <see cref="PageSizes"/>.
/// </summary>
public sealed class PageCursorTests
{
    /// <summary>
    /// A cursor survives being written and read again, down to the tick.
    /// </summary>
    [Fact]
    public void Encode_ThenTryParse_ReturnsTheSameCursor()
    {
        var cursor = new PageCursor(new DateTimeOffset(2026, 10, 1, 12, 30, 15, 123, TimeSpan.Zero).AddTicks(4567), Guid.CreateVersion7());

        PageCursor.TryParse(cursor.Encode(), out var parsed).ShouldBeTrue();

        parsed.ShouldBe(cursor);
    }

    /// <summary>
    /// Text that is not a cursor this API wrote is refused.
    /// </summary>
    /// <param name="text">The text to read.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("-5.0197a00000007000800000000000000a")]
    [InlineData("99999999999999999999999.0197a00000007000800000000000000a")]
    [InlineData("3155378976000000000.0197a00000007000800000000000000a")]
    [InlineData("1000.not-a-guid")]
    [InlineData("1000.0197a000-0000-7000-8000-00000000000a")]
    public void TryParse_WithTextThatIsNotACursor_ReturnsFalse(string? text)
    {
        PageCursor.TryParse(text, out var cursor).ShouldBeFalse();

        cursor.ShouldBe(default);
    }

    /// <summary>
    /// A page has the default size without a request, and the request is kept between one and the maximum.
    /// </summary>
    /// <param name="requested">The size asked for.</param>
    /// <param name="expected">The size the API uses.</param>
    [Theory]
    [InlineData(null, 5)]
    [InlineData(10, 10)]
    [InlineData(0, 1)]
    [InlineData(-3, 1)]
    [InlineData(500, 50)]
    public void Resolve_KeepsThePageSizeWithinBounds(int? requested, int expected) => PageSizes.Resolve(requested).ShouldBe(expected);
}
