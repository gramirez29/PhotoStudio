using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Domain.UnitTests.Notifications;

/// <summary>
/// Tests of <see cref="PushDevice"/>.
/// </summary>
public sealed class PushDeviceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Both spellings of an Expo push token are accepted.
    /// </summary>
    /// <param name="token">Token to check.</param>
    [Theory]
    [InlineData("ExponentPushToken[xxxxxxxxxxxxxxxxxxxxxx]")]
    [InlineData("ExpoPushToken[abc-DEF_123]")]
    public void IsValidToken_ForExpoTokens_IsTrue(string token) => PushDevice.IsValidToken(token).ShouldBeTrue();

    /// <summary>
    /// Anything that is not an Expo push token is refused, including an empty value, a bare value and one that never closes.
    /// </summary>
    /// <param name="token">Token to check.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ExponentPushToken[]")]
    [InlineData("ExponentPushToken[abc")]
    [InlineData("fcm-token-without-wrapper")]
    [InlineData("OtherPushToken[abc]")]
    public void IsValidToken_ForAnythingElse_IsFalse(string? token) => PushDevice.IsValidToken(token).ShouldBeFalse();

    /// <summary>
    /// A token longer than any real one is refused.
    /// </summary>
    [Fact]
    public void IsValidToken_ForAnAbsurdlyLongToken_IsFalse() =>
        PushDevice.IsValidToken($"ExponentPushToken[{new string('x', 300)}]").ShouldBeFalse();

    /// <summary>
    /// Registering creates a device first and last seen now, with the token trimmed.
    /// </summary>
    [Fact]
    public void Register_CreatesTheDevice()
    {
        var photographerId = Guid.CreateVersion7();

        var device = PushDevice.Register("  ExponentPushToken[abc]  ", photographerId, PushPlatform.Android, Now);

        device.Token.ShouldBe("ExponentPushToken[abc]");
        device.PhotographerId.ShouldBe(photographerId);
        device.Platform.ShouldBe(PushPlatform.Android);
        device.RegisteredAt.ShouldBe(Now);
        device.LastSeenAt.ShouldBe(Now);
    }

    /// <summary>
    /// A token that is not an Expo push token is refused with its own error code.
    /// </summary>
    [Fact]
    public void Register_WithAnInvalidToken_Fails()
    {
        var exception = Should.Throw<DomainException>(() => PushDevice.Register("nope", Guid.CreateVersion7(), PushPlatform.Android, Now));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidPushToken);
    }

    /// <summary>
    /// A device needs a photographer.
    /// </summary>
    [Fact]
    public void Register_WithoutAPhotographer_Fails()
    {
        var exception = Should.Throw<DomainException>(() => PushDevice.Register("ExponentPushToken[abc]", Guid.Empty, PushPlatform.Ios, Now));

        exception.Code.ShouldBe(DomainErrorCodes.RequiredValue);
    }
}
