namespace PhotoStudio.Api.Studio;

/// <summary>
/// The cookies of the web area. All of them are <c>HttpOnly</c> (the page cannot read them, so a script injected into it cannot steal
/// them), <c>Secure</c>, <c>SameSite=Strict</c> (the browser sends them only from our own site) and bound to the API host with no
/// <c>Domain</c> and the <c>__Host-</c> prefix, which makes the browser refuse any attempt to set them from a subdomain or over http.
/// Only in Development, over plain http on this machine, the cookies lose <c>Secure</c> and the prefix, because the browser would
/// not store them otherwise. In every other environment an insecure request never gets an insecure cookie.
/// </summary>
/// <param name="isDevelopment">Whether the API runs in the Development environment.</param>
public sealed class StudioCookies(bool isDevelopment)
{
    /// <summary>Kind of the cookie that holds the session.</summary>
    public const string Session = "session";

    /// <summary>Kind of the cookie that remembers the browser, so the code is not asked again for a while.</summary>
    public const string Device = "device";

    /// <summary>Kind of the cookie that ties a code to the browser that gave the password.</summary>
    public const string Challenge = "challenge";

    /// <summary>
    /// Tells whether cookies for this request are <c>Secure</c>: always, except over http in Development.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when the cookies carry <c>Secure</c> and the <c>__Host-</c> prefix.</returns>
    public bool IsSecure(HttpRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.IsHttps || !isDevelopment;
    }

    /// <summary>
    /// Gives the name a cookie has for this request.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="kind">One of <see cref="Session"/>, <see cref="Device"/> or <see cref="Challenge"/>.</param>
    /// <returns>The cookie name.</returns>
    public string NameOf(HttpRequest request, string kind) => IsSecure(request) ? $"__Host-ps-{kind}" : $"ps-{kind}";

    /// <summary>
    /// Reads a cookie.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="kind">Kind of the cookie.</param>
    /// <returns>The value, or <see langword="null"/> when the browser did not send it.</returns>
    public string? Read(HttpRequest request, string kind) =>
        request.Cookies.TryGetValue(NameOf(request, kind), out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// Sets a cookie that lives until an instant.
    /// </summary>
    /// <param name="context">The request being answered.</param>
    /// <param name="kind">Kind of the cookie.</param>
    /// <param name="value">The secret.</param>
    /// <param name="expiresAt">Instant the browser drops the cookie.</param>
    public void Set(HttpContext context, string kind, string value, DateTimeOffset expiresAt) =>
        context.Response.Cookies.Append(NameOf(context.Request, kind), value, Options(context.Request, expiresAt));

    /// <summary>
    /// Tells the browser to drop a cookie.
    /// </summary>
    /// <param name="context">The request being answered.</param>
    /// <param name="kind">Kind of the cookie.</param>
    public void Clear(HttpContext context, string kind) =>
        context.Response.Cookies.Delete(NameOf(context.Request, kind), Options(context.Request, null));

    /// <summary>
    /// Builds the attributes every cookie of the web area has.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="expiresAt">Instant the browser drops the cookie, if it has one.</param>
    /// <returns>The attributes.</returns>
    private CookieOptions Options(HttpRequest request, DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = IsSecure(request),
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
        Expires = expiresAt,
    };
}
