namespace PhotoStudio.Application.Pagination;

/// <summary>
/// How many items a page of a list has.
/// </summary>
public static class PageSizes
{
    /// <summary>
    /// Items in a page when the client does not say.
    /// </summary>
    public const int Default = 5;

    /// <summary>
    /// The most items a client can ask for in one page.
    /// </summary>
    public const int Max = 50;

    /// <summary>
    /// Turns what the client asked for into a size the API accepts.
    /// </summary>
    /// <param name="requested">The size asked for, or <see langword="null"/> when none was.</param>
    /// <returns>The default size without a request; otherwise the request kept between 1 and <see cref="Max"/>.</returns>
    public static int Resolve(int? requested) => requested is null ? Default : Math.Clamp(requested.Value, 1, Max);
}
