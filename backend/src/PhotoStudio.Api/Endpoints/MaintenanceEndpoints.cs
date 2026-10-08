using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Api.RateLimiting;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Maintenance.RunMaintenance;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// Endpoints that trigger background work on demand, the same work the scheduled worker runs once a day.
/// </summary>
public static class MaintenanceEndpoints
{
    /// <summary>
    /// Maps the maintenance endpoints under <c>/api/maintenance</c>.
    /// </summary>
    /// <param name="endpoints">Route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapMaintenanceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/maintenance").WithTags("Maintenance");

        group.MapPost("/run", RunAsync)
            .WithName("RunMaintenance")
            .RequireRateLimiting(RateLimitingExtensions.MaintenancePolicy);

        return endpoints;
    }

    /// <summary>
    /// Runs one maintenance pass: expires the tentative bookings whose hold ended and delivers the pending outbox messages.
    /// The pass is idempotent, so calling it again is harmless; the rate limiter keeps repeated calls from loading the database.
    /// </summary>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with what the pass did.</returns>
    private static async Task<Ok<MaintenanceResponse>> RunAsync(
        ICommandHandler<RunMaintenanceCommand, MaintenanceResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new RunMaintenanceCommand(), cancellationToken));
}
