using System.Globalization;

namespace PhotoStudio.Application.Pagination;

/// <summary>
/// Position in a list ordered from the newest item to the oldest: the instant of the last item the client already has and its
/// identifier, which breaks the tie between two items of the same instant. The next page starts right after it, so an item that
/// appears while the client is reading never shifts the pages (as an offset would).
/// </summary>
/// <param name="At">Instant the list is ordered by (UTC) of the last item already received.</param>
/// <param name="Id">Identifier of that item.</param>
public readonly record struct PageCursor(DateTimeOffset At, Guid Id)
{
    /// <summary>
    /// Largest number of ticks a cursor can carry: the last instant a date can hold.
    /// </summary>
    private const long MaxTicks = 3_155_378_975_999_999_999;

    /// <summary>
    /// Writes the cursor as the opaque text the API hands to the client.
    /// </summary>
    /// <returns>The text, to be sent back unchanged to get the next page.</returns>
    public string Encode() => string.Create(CultureInfo.InvariantCulture, $"{At.UtcTicks}.{Id:N}");

    /// <summary>
    /// Reads a cursor the client sent back.
    /// </summary>
    /// <param name="value">The text, or <see langword="null"/> when the client asks for the first page.</param>
    /// <param name="cursor">The cursor when the text is valid; otherwise the default value.</param>
    /// <returns><see langword="true"/> when the text is a cursor this API wrote.</returns>
    public static bool TryParse(string? value, out PageCursor cursor)
    {
        cursor = default;
        var parts = value?.Split('.');
        if (parts is not [var ticksText, var idText]
            || !long.TryParse(ticksText, NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
            || ticks > MaxTicks
            || !Guid.TryParseExact(idText, "N", out var id))
        {
            return false;
        }

        cursor = new PageCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
        return true;
    }
}
