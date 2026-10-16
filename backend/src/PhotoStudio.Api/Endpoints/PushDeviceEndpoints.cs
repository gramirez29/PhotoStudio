using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Api.Auth;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Notifications.Push;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// The phones of the signed-in photographer that receive push notifications. The app registers its phone when the
/// photographer signs in and forgets it when they sign out. Every operation is scoped to the photographer of the token.
/// </summary>
public static class PushDeviceEndpoints
{
    /// <summary>
    /// Maps the push device endpoints under <c>/api/push-devices</c>.
    /// </summary>
    /// <param name="endpoints">Route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapPushDeviceEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/push-devices").WithTags("Push devices").RequireAuthorization();

        group.MapPost("/", RegisterAsync).WithName("RegisterPushDevice");
        group.MapPost("/unregister", UnregisterAsync).WithName("UnregisterPushDevice");

        return endpoints;
    }

    /// <summary>
    /// Registers the phone for push notifications. Registering the same phone again is harmless; a phone that was registered
    /// by another photographer moves to this one.
    /// </summary>
    /// <param name="request">Phone to register.</param>
    /// <param name="user">Authenticated photographer.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>204; 422 <c>push.invalid_token</c> when the token is not an Expo push token.</returns>
    private static async Task<NoContent> RegisterAsync(
        RegisterPushDeviceRequest request,
        ClaimsPrincipal user,
        ICommandHandler<RegisterPushDeviceCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request.ToCommand(user.GetPhotographerId()), cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// Stops sending push notifications to the phone. A token that is unknown or belongs to someone else is ignored.
    /// </summary>
    /// <param name="request">Phone to forget.</param>
    /// <param name="user">Authenticated photographer.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>204.</returns>
    private static async Task<NoContent> UnregisterAsync(
        UnregisterPushDeviceRequest request,
        ClaimsPrincipal user,
        ICommandHandler<UnregisterPushDeviceCommand, bool> handler,
        CancellationToken cancellationToken)
    {
        await handler.HandleAsync(request.ToCommand(user.GetPhotographerId()), cancellationToken);
        return TypedResults.NoContent();
    }
}
