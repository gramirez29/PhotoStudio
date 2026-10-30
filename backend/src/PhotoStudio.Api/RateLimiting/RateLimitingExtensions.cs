using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace PhotoStudio.Api.RateLimiting;

/// <summary>
/// Rate limiting policies of the API. Rejected requests get a 429 problem details response with a stable <c>code</c>.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Name of the policy of the on-demand maintenance endpoint: one call per minute for the whole API.
    /// </summary>
    public const string MaintenancePolicy = "maintenance";

    /// <summary>
    /// Name of the policy of the sign-in endpoints: 10 calls per minute per client address.
    /// </summary>
    public const string AuthPolicy = "auth";

    /// <summary>
    /// Name of the policy of the sign-up endpoint: 5 accounts per hour per client address. It is stricter than the sign-in
    /// policy because creating accounts is the cheapest way to fill the database.
    /// </summary>
    public const string RegisterPolicy = "register";

    /// <summary>
    /// Name of the policy of the portal endpoint that reads a booking: 60 calls per minute per client address.
    /// </summary>
    public const string PortalReadPolicy = "portal-read";

    /// <summary>
    /// Name of the policy of the portal endpoints that send and check verification codes: 10 calls per minute per client
    /// address. Every booking is also limited on its own, because the address is the only thing an attacker can change.
    /// </summary>
    public const string PortalCodePolicy = "portal-code";

    /// <summary>
    /// Name of the policy of the portal endpoints that change a booking: 10 calls per minute per client address.
    /// </summary>
    public const string PortalActionPolicy = "portal-action";

    /// <summary>
    /// Name of the policy of the actions of the web area that move proofs (ask for places, register, remove, open the selection): per address.
    /// </summary>
    public const string StudioUploadPolicy = "studio-upload";

    /// <summary>
    /// Stable error code returned when a request is rejected by the rate limiter.
    /// </summary>
    public const string RateLimitExceededCode = "rate_limit.exceeded";

    /// <summary>
    /// Registers the rate limiter and its policies.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApiRateLimiting(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddFixedWindowLimiter(MaintenancePolicy, window =>
            {
                window.PermitLimit = 1;
                window.Window = TimeSpan.FromMinutes(1);
                window.QueueLimit = 0;
            });
            options.AddPolicy(AuthPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
            options.AddPolicy(RegisterPolicy, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                }));
            AddPerAddressPolicy(options, PortalReadPolicy, 60);
            AddPerAddressPolicy(options, PortalCodePolicy, 10);
            AddPerAddressPolicy(options, PortalActionPolicy, 10);
            AddPerAddressPolicy(options, StudioUploadPolicy, 60);
            options.OnRejected = WriteRejectionAsync;
        });
    }

    /// <summary>
    /// Adds a policy that allows a number of calls per minute to each client address.
    /// </summary>
    /// <param name="options">Rate limiter options.</param>
    /// <param name="name">Policy name.</param>
    /// <param name="permitsPerMinute">Calls allowed per minute for one address.</param>
    private static void AddPerAddressPolicy(RateLimiterOptions options, string name, int permitsPerMinute) =>
        options.AddPolicy(name, httpContext => RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));

    /// <summary>
    /// Writes the 429 response as problem details, with the <c>Retry-After</c> header when the limiter knows it.
    /// </summary>
    /// <param name="context">Rejected request.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task that completes when the response is written.</returns>
    private static async ValueTask WriteRejectionAsync(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var httpContext = context.HttpContext;
        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too many requests.",
            Detail = "This action was requested too many times. Wait a while and try again.",
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["code"] = RateLimitExceededCode;

        await httpContext.RequestServices.GetRequiredService<IProblemDetailsService>().TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
        });
    }
}
