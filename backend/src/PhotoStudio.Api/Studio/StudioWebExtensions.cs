using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors.Infrastructure;
using PhotoStudio.Api.Cors;
using PhotoStudio.Application.Portal;

namespace PhotoStudio.Api.Studio;

/// <summary>
/// The sites that may use the web area of the photographer, and how the API defends it. The web area is the only part of the API
/// that a browser uses with a cookie, so it is the only one exposed to a request that a malicious page makes with the cookie of the
/// user: it is defended with a cookie that is never sent from another site (<c>SameSite=Strict</c>), a CORS policy that names the one
/// site and allows credentials only for it, and a check of the <c>Origin</c> of every request that changes something.
/// </summary>
/// <param name="AllowedOrigins">The origins (scheme, host and port) of the site that may call the web area.</param>
public sealed record StudioWebSettings(IReadOnlyList<string> AllowedOrigins)
{
    /// <summary>Name of the CORS policy of the web area.</summary>
    public const string CorsPolicyName = "studio-web";

    /// <summary>Name of the authorization policy of the web area: a live session, with the scope of the web area.</summary>
    public const string Policy = "studio-web";

    /// <summary>Path under which everything belongs to the web area.</summary>
    public const string PathPrefix = "/api/web";
}

/// <summary>
/// Registers the web area: its cookies, its authentication scheme, its policy and its CORS policy.
/// </summary>
public static class StudioWebExtensions
{
    /// <summary>
    /// Registers the web area of the photographer.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="isDevelopment">Whether the API runs in the Development environment.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddStudioWeb(this IServiceCollection services, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new StudioCookies(isDevelopment));

        // Only the site of the portal (and, in Development, this machine): the extra origins of the client portal do not get credentials.
        services.AddSingleton(provider => new StudioWebSettings(PortalCorsExtensions.ResolveOrigins(provider.GetRequiredService<PortalSettings>().BaseUrl, [], isDevelopment)));

        services.AddAuthentication().AddScheme<AuthenticationSchemeOptions, StudioSessionAuthenticationHandler>(StudioSessionAuthenticationHandler.SchemeName, _ => { });

        // Only this scheme is consulted, and only a session of the web area passes: a token of the app has no such scope, and the
        // cookie is not accepted anywhere else because the other endpoints consult only the bearer schemes.
        services.AddAuthorizationBuilder().AddPolicy(StudioWebSettings.Policy, policy => policy
            .AddAuthenticationSchemes(StudioSessionAuthenticationHandler.SchemeName)
            .RequireAuthenticatedUser()
            .RequireClaim(StudioSessionAuthenticationHandler.ScopeClaimType, StudioSessionAuthenticationHandler.StudioScope));

        services.AddOptions<CorsOptions>().Configure<StudioWebSettings>((options, settings) =>
            options.AddPolicy(StudioWebSettings.CorsPolicyName, policy => policy
                .WithOrigins([.. settings.AllowedOrigins])
                .WithMethods(HttpMethods.Get, HttpMethods.Post)
                .WithHeaders("Content-Type")
                .AllowCredentials()
                .WithExposedHeaders("Retry-After")
                .SetPreflightMaxAge(TimeSpan.FromHours(1))));

        return services;
    }

    /// <summary>
    /// Guards the web area: no answer under it is ever stored by a cache, and a request that changes something must come from the
    /// site of the web area. A browser always sends <c>Origin</c> on those requests, so one without it, or with another, is not our
    /// page: it is refused with 403 before anything is read.
    /// </summary>
    /// <param name="app">The application.</param>
    /// <returns>The same application, for chaining.</returns>
    public static IApplicationBuilder UseStudioWebGuard(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.Use(async (context, next) =>
        {
            if (!context.Request.Path.StartsWithSegments(StudioWebSettings.PathPrefix))
            {
                await next(context);
                return;
            }

            context.Response.Headers.CacheControl = "no-store";
            if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method) || HttpMethods.IsOptions(context.Request.Method))
            {
                await next(context);
                return;
            }

            var settings = context.RequestServices.GetRequiredService<StudioWebSettings>();
            var origin = context.Request.Headers.Origin.ToString();
            if (settings.AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                type = "about:blank",
                title = "The request does not come from the site of the web area.",
                status = StatusCodes.Status403Forbidden,
                code = "request.forbidden_origin",
            }));
        });
    }
}
