using PhotoStudio.Application.Notifications.Push;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// Body of <c>POST /api/push-devices</c>: the phone that should receive push notifications.
/// </summary>
/// <param name="Token">Expo push token of the phone, for example <c>ExponentPushToken[...]</c>.</param>
/// <param name="Platform">Operating system of the phone (<c>Android</c> or <c>Ios</c>).</param>
public sealed record RegisterPushDeviceRequest(string? Token, PushPlatform Platform)
{
    /// <summary>
    /// Maps the request to its command.
    /// </summary>
    /// <param name="photographerId">Photographer taken from the access token.</param>
    /// <returns>The command.</returns>
    public RegisterPushDeviceCommand ToCommand(Guid photographerId) => new(photographerId, Token ?? string.Empty, Platform);
}

/// <summary>
/// Body of <c>POST /api/push-devices/unregister</c>: the phone that should stop receiving push notifications.
/// </summary>
/// <param name="Token">Expo push token of the phone.</param>
public sealed record UnregisterPushDeviceRequest(string? Token)
{
    /// <summary>
    /// Maps the request to its command.
    /// </summary>
    /// <param name="photographerId">Photographer taken from the access token.</param>
    /// <returns>The command.</returns>
    public UnregisterPushDeviceCommand ToCommand(Guid photographerId) => new(photographerId, Token ?? string.Empty);
}
