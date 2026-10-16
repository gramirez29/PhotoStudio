using System.Diagnostics.CodeAnalysis;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Notifications;

/// <summary>
/// Operating system of a device that receives push notifications.
/// </summary>
public enum PushPlatform
{
    /// <summary>An Android device.</summary>
    Android,

    /// <summary>An iOS device.</summary>
    Ios,
}

/// <summary>
/// A phone of a photographer that can receive push notifications, identified by its Expo push token. A token belongs to one
/// photographer at a time: when another photographer signs in on the same phone, registering the token again moves it.
/// </summary>
public sealed class PushDevice
{
    /// <summary>
    /// Prefixes an Expo push token starts with, followed by the value in square brackets.
    /// </summary>
    private static readonly string[] TokenPrefixes = ["ExponentPushToken[", "ExpoPushToken["];

    /// <summary>
    /// Longest token accepted, a generous bound over the real length (about 40 characters).
    /// </summary>
    private const int MaxTokenLength = 200;

    /// <summary>
    /// Initializes a new instance of the <see cref="PushDevice"/> class. Use <see cref="Register"/> or <see cref="Restore"/>.
    /// </summary>
    private PushDevice(string token, Guid photographerId, PushPlatform platform, DateTimeOffset registeredAt, DateTimeOffset lastSeenAt)
    {
        Token = token;
        PhotographerId = photographerId;
        Platform = platform;
        RegisteredAt = registeredAt;
        LastSeenAt = lastSeenAt;
    }

    /// <summary>
    /// Gets the Expo push token, which identifies the device.
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Gets the photographer (tenant) the device belongs to.
    /// </summary>
    public Guid PhotographerId { get; }

    /// <summary>
    /// Gets the operating system of the device.
    /// </summary>
    public PushPlatform Platform { get; }

    /// <summary>
    /// Gets the instant the device was first registered (UTC).
    /// </summary>
    public DateTimeOffset RegisteredAt { get; }

    /// <summary>
    /// Gets the instant the device registered last (UTC); the app registers on every sign-in.
    /// </summary>
    public DateTimeOffset LastSeenAt { get; }

    /// <summary>
    /// Indicates whether a text has the shape of an Expo push token.
    /// </summary>
    /// <param name="token">Text to check.</param>
    /// <returns><see langword="true"/> for <c>ExponentPushToken[...]</c> and <c>ExpoPushToken[...]</c>.</returns>
    public static bool IsValidToken([NotNullWhen(true)] string? token) =>
        !string.IsNullOrWhiteSpace(token)
        && token.Length <= MaxTokenLength
        && token.EndsWith(']')
        && TokenPrefixes.Any(prefix => token.StartsWith(prefix, StringComparison.Ordinal) && token.Length > prefix.Length + 1);

    /// <summary>
    /// Registers a device for a photographer.
    /// </summary>
    /// <param name="token">Expo push token of the device.</param>
    /// <param name="photographerId">Photographer the device belongs to.</param>
    /// <param name="platform">Operating system of the device.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The device.</returns>
    /// <exception cref="DomainException">When the photographer is missing or the token is not an Expo push token.</exception>
    public static PushDevice Register(string token, Guid photographerId, PushPlatform platform, DateTimeOffset now)
    {
        if (photographerId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The photographer of a device is required.");
        }

        var trimmed = token?.Trim();
        if (!IsValidToken(trimmed))
        {
            throw new DomainException(DomainErrorCodes.InvalidPushToken, "The push token is not a valid Expo push token.");
        }

        return new PushDevice(trimmed, photographerId, platform, now, now);
    }

    /// <summary>
    /// Rebuilds a device from persisted data, without validating it again.
    /// </summary>
    /// <param name="token">Expo push token.</param>
    /// <param name="photographerId">Photographer.</param>
    /// <param name="platform">Operating system.</param>
    /// <param name="registeredAt">First registration instant.</param>
    /// <param name="lastSeenAt">Last registration instant.</param>
    /// <returns>The restored device.</returns>
    public static PushDevice Restore(string token, Guid photographerId, PushPlatform platform, DateTimeOffset registeredAt, DateTimeOffset lastSeenAt) =>
        new(token, photographerId, platform, registeredAt, lastSeenAt);
}
