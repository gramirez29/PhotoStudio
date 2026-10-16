using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Portal;
using PhotoStudio.Infrastructure.Email;
using PhotoStudio.Infrastructure.Identity;

namespace PhotoStudio.Infrastructure.Portal;

/// <summary>
/// Registers what the API needs for the client portal: the portal settings, the verification codes, the email provider and
/// the authentication of portal sessions, which is a second JWT scheme kept apart from the photographer's.
/// </summary>
public static class ClientPortalExtensions
{
    /// <summary>
    /// Environment variable that holds the public address of the portal.
    /// </summary>
    public const string PortalBaseUrlVariable = "PORTAL_BASE_URL";

    /// <summary>
    /// Address used in Development when the variable is not set: the local server of the portal (Vite). Any other environment
    /// must set the variable, because a default would silently put a wrong address in every link sent to a client.
    /// </summary>
    public const string DevelopmentPortalBaseUrl = "http://localhost:5173";

    /// <summary>
    /// Name of the authentication scheme of portal sessions.
    /// </summary>
    public const string PortalScheme = "PortalBearer";

    /// <summary>
    /// Name of the authorization policy of the endpoints a client calls with a portal session.
    /// </summary>
    public const string ClientPortalPolicy = "client-portal";

    /// <summary>
    /// Adds the client portal services. Only the API calls this: the worker has no endpoints.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="isDevelopment">Whether the API runs in the Development environment.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">When the token settings or the email settings in the environment are invalid.</exception>
    public static IServiceCollection AddClientPortal(this IServiceCollection services, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = JwtOptions.FromEnvironment(isDevelopment);
        services.AddSingleton(new PortalSettings(ReadPortalBaseUrl(isDevelopment)));
        services.AddSingleton<IOtpCodeService, OtpCodeService>();
        services.AddSingleton<IPortalSessionIssuer, PortalSessionTokenIssuer>();
        AddEmailSender(services, isDevelopment);

        services
            .AddAuthentication()
            .AddJwtBearer(PortalScheme, bearer =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = PortalSessionTokenIssuer.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtAccessTokenIssuer.CreateKey(options),
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = PhotographerAuthenticationExtensions.ClockSkew,
                    NameClaimType = PortalSessionTokenIssuer.BookingClaimType,
                };
            });

        // Only a valid portal token passes, and only that scheme is consulted: a photographer token fails on its audience.
        services.AddAuthorizationBuilder()
            .AddPolicy(ClientPortalPolicy, policy => policy
                .AddAuthenticationSchemes(PortalScheme)
                .RequireAuthenticatedUser()
                .RequireClaim(PortalSessionTokenIssuer.BookingClaimType));

        return services;
    }

    /// <summary>
    /// Gets the booking a portal session was issued for.
    /// </summary>
    /// <param name="user">The authenticated user of the request.</param>
    /// <returns>The booking identifier.</returns>
    /// <exception cref="InvalidOperationException">When the token carries no valid booking. A token this API issued always does.</exception>
    public static Guid GetPortalBookingId(this System.Security.Claims.ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var value = user.FindFirst(PortalSessionTokenIssuer.BookingClaimType)?.Value;
        return Guid.TryParse(value, out var bookingId) && bookingId != Guid.Empty
            ? bookingId
            : throw new InvalidOperationException("The portal token has no valid booking.");
    }

    /// <summary>
    /// Registers the email provider: Resend when its key is set, the log sender in Development, and a sender that fails
    /// loudly otherwise.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="isDevelopment">Whether the API runs in the Development environment.</param>
    private static void AddEmailSender(IServiceCollection services, bool isDevelopment)
    {
        var email = EmailOptions.FromEnvironment();
        if (email is not null)
        {
            services.AddSingleton(email);
            services.AddHttpClient<IEmailSender, ResendEmailSender>(client =>
            {
                client.BaseAddress = new Uri(ResendEmailSender.BaseAddress);
                client.Timeout = TimeSpan.FromSeconds(10);
            });
        }
        else if (isDevelopment)
        {
            services.AddSingleton<IEmailSender, LogEmailSender>();
        }
        else
        {
            services.AddSingleton<IEmailSender, UnavailableEmailSender>();
        }
    }

    /// <summary>
    /// Reads the public address of the portal, without a trailing slash.
    /// </summary>
    /// <param name="isDevelopment">Whether the API runs in the Development environment.</param>
    /// <returns>The address.</returns>
    /// <exception cref="InvalidOperationException">When the variable is missing outside Development, or is not an absolute http or https address.</exception>
    private static string ReadPortalBaseUrl(bool isDevelopment)
    {
        var value = Environment.GetEnvironmentVariable(PortalBaseUrlVariable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return isDevelopment
                ? DevelopmentPortalBaseUrl
                : throw new InvalidOperationException($"The environment variable '{PortalBaseUrlVariable}' is not set; it is the public address of the site, used in the links sent to clients.");
        }

        var trimmed = value.Trim().TrimEnd('/');
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException($"'{PortalBaseUrlVariable}' must be an absolute http or https address.");
        }

        return trimmed;
    }
}
