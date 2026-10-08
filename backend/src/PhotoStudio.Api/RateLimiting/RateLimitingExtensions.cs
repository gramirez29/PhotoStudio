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
            options.OnRejected = WriteRejectionAsync;
        });
    }

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
