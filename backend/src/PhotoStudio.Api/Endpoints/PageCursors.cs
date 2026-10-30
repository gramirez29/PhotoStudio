using PhotoStudio.Application.Pagination;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// Reads the <c>cursor</c> query parameter of the paged lists.
/// </summary>
internal static class PageCursors
{
    /// <summary>
    /// Reads the cursor the client sent back.
    /// </summary>
    /// <param name="cursor">The text of the query parameter, or <see langword="null"/> when it was not sent.</param>
    /// <returns>The cursor, or <see langword="null"/> for the first page.</returns>
    /// <exception cref="BadHttpRequestException">When the text is not a cursor this API wrote (answered as 400 <c>request.invalid</c>).</exception>
    public static PageCursor? Read(string? cursor)
    {
        if (cursor is null)
        {
            return null;
        }

        return PageCursor.TryParse(cursor, out var parsed)
            ? parsed
            : throw new BadHttpRequestException("The cursor is not valid.");
    }
}
