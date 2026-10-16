using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.Notifications.Push;

/// <summary>
/// Command to register the phone of the signed-in photographer for push notifications.
/// </summary>
/// <param name="PhotographerId">Photographer taken from the access token.</param>
/// <param name="Token">Expo push token of the phone.</param>
/// <param name="Platform">Operating system of the phone.</param>
public sealed record RegisterPushDeviceCommand(Guid PhotographerId, string Token, PushPlatform Platform);

/// <summary>
/// Command to stop notifying a phone of the signed-in photographer, for example when they sign out.
/// </summary>
/// <param name="PhotographerId">Photographer taken from the access token.</param>
/// <param name="Token">Expo push token of the phone.</param>
public sealed record UnregisterPushDeviceCommand(Guid PhotographerId, string Token);
