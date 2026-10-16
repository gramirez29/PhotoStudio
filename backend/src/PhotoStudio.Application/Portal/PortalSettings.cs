namespace PhotoStudio.Application.Portal;

/// <summary>
/// Settings of the client portal.
/// </summary>
/// <param name="BaseUrl">Public address of the portal, without a trailing slash, for example <c>https://develop.photosensestudio.com</c>.</param>
public sealed record PortalSettings(string BaseUrl)
{
    /// <summary>
    /// How long a booking link works after the session ends, so the client can still pay the balance and reach the gallery.
    /// </summary>
    public static readonly TimeSpan LinkLifetimeAfterSession = TimeSpan.FromDays(90);

    /// <summary>
    /// Builds the URL a client opens for a link token.
    /// </summary>
    /// <param name="token">Secret token of the link.</param>
    /// <returns>The URL.</returns>
    public string LinkUrl(string token) => $"{BaseUrl}/b/{token}";
}
