using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Identity;

namespace PhotoStudio.Infrastructure.Identity;

/// <summary>
/// Registers everything the API needs to authenticate users: the token settings, the access token issuer, the JWT
/// validation, a fallback policy that makes every endpoint require authentication unless it opts out, and the sign-up switch.
/// </summary>
public static class PhotographerAuthenticationExtensions
{
    /// <summary>
    /// Environment variable that turns sign-up off when its value is <c>false</c>. Sign-up is open when it is not set.
    /// </summary>
    public const string RegistrationEnabledVariable = "AUTH_REGISTRATION_ENABLED";

    /// <summary>
    /// Name of the authorization policy of the endpoints that run work for every photographer. Only users with the system
    /// operator role in their token pass it.
    /// </summary>
    public const string SystemOperatorPolicy = "system-operator";

    /// <summary>
    /// Name of the variable that turns the email verification requirement on or off (on by default).
    /// </summary>
    public const string EmailVerificationRequiredVariable = "AUTH_EMAIL_VERIFICATION_REQUIRED";

    /// <summary>
    /// Name of the policy of the endpoints that a user who has not verified their email can still use (the ones that verify it).
    /// </summary>
    public const string AnyUserPolicy = "any-user";

    /// <summary>
    /// Maximum difference tolerated between the clock of the API and the expiry of a token.
    /// </summary>
    public static readonly TimeSpan ClockSkew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Adds JWT authentication for the photographer app. Only the API calls this: the worker has no endpoints, so it does
    /// not need the signing key.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <param name="isDevelopment">Whether the API runs in the Development environment.</param>
    /// <returns>The same service collection, for chaining.</returns>
    /// <exception cref="InvalidOperationException">When the token settings in the environment are missing or invalid.</exception>
    public static IServiceCollection AddPhotographerAuthentication(this IServiceCollection services, bool isDevelopment)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = JwtOptions.FromEnvironment(isDevelopment);
        services.AddSingleton(options);
        services.AddSingleton(new AuthSessionSettings(options.RefreshTokenLifetime));
        services.AddSingleton(new RegistrationSettings(ReadBoolean(RegistrationEnabledVariable)));
        services.AddSingleton(new EmailVerificationSettings(ReadBoolean(EmailVerificationRequiredVariable)));
        services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                // Keep the claim names as issued ("sub", "preferred_username") instead of mapping them to the legacy WS-Federation URIs.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = options.Issuer,
                    ValidateAudience = true,
                    ValidAudience = options.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtAccessTokenIssuer.CreateKey(options),
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,

                    // Only the algorithm we sign with: a token claiming "none" or another algorithm is refused.
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ClockSkew = ClockSkew,
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                };
            });

        // Secure by default: an endpoint is public only when it says so with AllowAnonymous. Both the fallback policy (endpoints with
        // no policy of their own) and the default one (RequireAuthorization() with no name) also keep out a user who has not verified
        // their email; only the endpoints that verify it use AnyUserPolicy.
        var verifiedUser = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().RequireAssertion(HasNotBeenMarkedUnverified).Build();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(verifiedUser)
            .SetDefaultPolicy(verifiedUser)
            .AddPolicy(AnyUserPolicy, policy => policy.RequireAuthenticatedUser())
            .AddPolicy(SystemOperatorPolicy, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(HasNotBeenMarkedUnverified)
                .RequireClaim(JwtAccessTokenIssuer.SystemRoleClaimType, JwtAccessTokenIssuer.SystemRoleValue));

        return services;
    }

    /// <summary>
    /// Tells whether the token of the user was issued as "email not verified". A token with no such claim (one issued before
    /// verification existed, which lives at most fifteen minutes) is accepted, so deploying does not cut the sessions that are open.
    /// </summary>
    /// <param name="context">Authorization context of the request.</param>
    /// <returns><see langword="false"/> when the token says the email is not verified.</returns>
    private static bool HasNotBeenMarkedUnverified(AuthorizationHandlerContext context) =>
        !context.User.HasClaim(JwtAccessTokenIssuer.EmailVerifiedClaimType, "false");

    /// <summary>
    /// Reads a switch from the environment; it is on when the variable is missing. A value that is neither <c>true</c> nor <c>false</c>
    /// stops the API at startup: guessing could leave sign-up open, or verification off, when the operator meant the opposite.
    /// </summary>
    /// <param name="variable">Name of the environment variable.</param>
    /// <returns><see langword="true"/> unless the variable is set to <c>false</c>.</returns>
    /// <exception cref="InvalidOperationException">When the variable holds anything other than true or false.</exception>
    private static bool ReadBoolean(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (bool.TryParse(value.Trim(), out var enabled))
        {
            return enabled;
        }

        throw new InvalidOperationException($"'{variable}' must be 'true' or 'false'.");
    }
}
