using System.IdentityModel.Tokens.Jwt;
using PhotoStudio.Infrastructure.Identity;
using PhotoStudio.Infrastructure.Portal;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of how long the session of a client lasts after they confirm the code of their email.
/// </summary>
public sealed class PortalSessionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    /// <summary>Options with a key of the test; no environment variable is needed.</summary>
    private static readonly JwtOptions Options = new("test-only-signing-key-with-more-than-32-characters", "issuer", "audience", TimeSpan.FromMinutes(15), TimeSpan.FromDays(30));

    /// <summary>
    /// Without configuration a session lasts 30 days, so the first code serves for the contract, the payments and the gallery.
    /// </summary>
    [Fact]
    public void ByDefault_ASessionLastsThirtyDays()
    {
        PortalSessionLifetime.FromEnvironment(null).ShouldBe(TimeSpan.FromDays(30));
        PortalSessionLifetime.FromEnvironment("  ").ShouldBe(TimeSpan.FromDays(30));
    }

    /// <summary>
    /// The number of days can be set, from one to ninety, and anything else stops the start with a message that says so.
    /// </summary>
    /// <param name="value">What the variable says.</param>
    /// <param name="days">How many days it means.</param>
    [Theory]
    [InlineData("1", 1)]
    [InlineData(" 7 ", 7)]
    [InlineData("90", 90)]
    public void TheDays_CanBeSet(string value, int days) => PortalSessionLifetime.FromEnvironment(value).ShouldBe(TimeSpan.FromDays(days));

    /// <summary>
    /// A value that is not a whole number of days within the limits is refused, so a typo does not leave sessions that last forever.
    /// </summary>
    /// <param name="value">What the variable says.</param>
    [Theory]
    [InlineData("0")]
    [InlineData("91")]
    [InlineData("-3")]
    [InlineData("7.5")]
    [InlineData("a week")]
    public void AnInvalidValue_IsRefused(string value) =>
        Should.Throw<InvalidOperationException>(() => PortalSessionLifetime.FromEnvironment(value)).Message.ShouldContain("PORTAL_SESSION_DAYS");

    /// <summary>
    /// The token expires when the lifetime says, and belongs to the booking it was issued for.
    /// </summary>
    [Fact]
    public void TheToken_ExpiresAfterTheLifetime()
    {
        var issuer = new PortalSessionTokenIssuer(Options, TimeSpan.FromDays(30));
        var bookingId = Guid.CreateVersion7();

        var token = issuer.Issue(bookingId, Now);

        token.ExpiresAt.ShouldBe(Now.AddDays(30));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Value);
        new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero).ShouldBe(Now.AddDays(30), TimeSpan.FromSeconds(1));
        jwt.Claims.Single(claim => claim.Type == PortalSessionTokenIssuer.BookingClaimType).Value.ShouldBe(bookingId.ToString());
    }

    /// <summary>
    /// A session cannot be made to last no time at all.
    /// </summary>
    [Fact]
    public void ALifetimeOfNothing_IsRefused() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new PortalSessionTokenIssuer(Options, TimeSpan.Zero));
}
