using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Options;
using PhotoStudio.Application.Portal;

namespace PhotoStudio.Api.Cors;

/// <summary>
/// Cross-origin policy of the client portal. The portal is a web page served from its own address, so the browser only lets
/// it call the API when the API says so. The policy is attached to the <c>/public</c> endpoints alone: the photographer's API
/// is called by the mobile app, which has no origin, and must not be callable from a page on any other site.
/// </summary>
public static class PortalCorsExtensions
{
    /// <summary>
    /// Name of the policy applied to the portal endpoints.
    /// </summary>
    public const string PolicyName = "portal";

    /// <summary>
    /// Environment variable with extra allowed origins, separated by commas, for example a preview deployment of the portal.
    /// </summary>
    public const string ExtraOriginsVariable = "PORTAL_ORIGINS";

    /// <summary>
    /// Origins of the local development server of the portal (Vite), allowed in Development only.
    /// </summary>
    public static readonly IReadOnlyList<string> DevelopmentOrigins = ["http://localhost:5173", "http://127.0.0.1:5173"];

    /// <summary>
    /// Registers the portal policy. The allowed origins are the one of <see cref="PortalSettings.BaseUrl"/>, the ones in
    /// <see cref="ExtraOriginsVariable"/> and, in Development, <see cref="DevelopmentOrigins"/>. Nothing else is allowed, and
    /// there is no wildcard.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="isDevelopment">Whether the API runs in the Development environment.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">When an extra origin is not an absolute http or https origin.</exception>
    public static IServiceCollection AddPortalCors(this IServiceCollection services, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(services);

        var extraOrigins = ParseExtraOrigins(Environment.GetEnvironmentVariable(ExtraOriginsVariable));
        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<PortalSettings>((options, settings) =>
        {
            var origins = ResolveOrigins(settings.BaseUrl, extraOrigins, isDevelopment);
            options.AddPolicy(PolicyName, policy => policy
                .WithOrigins(origins)
                .WithMethods(HttpMethods.Get, HttpMethods.Post)
                .WithHeaders("Authorization", "Content-Type")
                .WithExposedHeaders("Retry-After")
                .SetPreflightMaxAge(TimeSpan.FromHours(1)));
        });

        return services;
    }

    /// <summary>
    /// Builds the list of allowed origins.
    /// </summary>
    /// <param name="portalBaseUrl">Public address of the portal.</param>
    /// <param name="extraOrigins">Extra origins from the environment.</param>
    /// <param name="isDevelopment">Whether the API runs in the Development environment.</param>
    /// <returns>The distinct origins, without paths or trailing slashes.</returns>
    public static string[] ResolveOrigins(string portalBaseUrl, IEnumerable<string> extraOrigins, bool isDevelopment)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portalBaseUrl);
        ArgumentNullException.ThrowIfNull(extraOrigins);

        var origins = new List<string> { new Uri(portalBaseUrl).GetLeftPart(UriPartial.Authority) };
        origins.AddRange(extraOrigins);
        if (isDevelopment)
        {
            origins.AddRange(DevelopmentOrigins);
        }

        return [.. origins.Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Reads the comma-separated extra origins. A value that is not an origin stops the API at startup: ignoring it would
    /// leave the portal blocked without a clue why.
    /// </summary>
    /// <param name="value">Value of the environment variable, or <see langword="null"/>.</param>
    /// <returns>The origins, without trailing slashes.</returns>
    /// <exception cref="InvalidOperationException">When an entry is not an absolute http or https origin.</exception>
    public static IReadOnlyList<string> ParseExtraOrigins(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var origins = new List<string>();
        foreach (var entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var trimmed = entry.TrimEnd('/');
            var isOrigin = Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                && uri.Scheme is "http" or "https"
                && uri.GetLeftPart(UriPartial.Authority).Equals(trimmed, StringComparison.OrdinalIgnoreCase);
            if (!isOrigin)
            {
                throw new InvalidOperationException($"'{ExtraOriginsVariable}' must list absolute http or https origins without a path; '{entry}' is not one.");
            }

            origins.Add(trimmed);
        }

        return origins;
    }
}
