using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Domain.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="RefreshToken"/>.
/// </summary>
public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// An issued token is unused and expires after its lifetime.
    /// </summary>
    [Fact]
    public void Issue_CreatesAnUnusedTokenThatExpiresAfterItsLifetime()
    {
        var photographerId = Guid.CreateVersion7();
        var familyId = Guid.CreateVersion7();

        var token = RefreshToken.Issue(photographerId, familyId, "hash", Now, TimeSpan.FromDays(30));

        token.PhotographerId.ShouldBe(photographerId);
        token.FamilyId.ShouldBe(familyId);
        token.TokenHash.ShouldBe("hash");
        token.CreatedAt.ShouldBe(Now);
        token.ExpiresAt.ShouldBe(Now.AddDays(30));
        token.IsRevoked.ShouldBeFalse();
        token.ReplacedById.ShouldBeNull();
        token.Id.ShouldNotBe(Guid.Empty);
    }

    /// <summary>
    /// The token is expired from its expiry instant on.
    /// </summary>
    [Fact]
    public void IsExpiredAt_IsTrueFromTheExpiryInstantOn()
    {
        var token = RefreshToken.Issue(Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now, TimeSpan.FromDays(1));

        token.IsExpiredAt(Now.AddDays(1).AddTicks(-1)).ShouldBeFalse();
        token.IsExpiredAt(Now.AddDays(1)).ShouldBeTrue();
    }

    /// <summary>
    /// A restored token with a revocation instant reports itself as revoked.
    /// </summary>
    [Fact]
    public void Restore_WithRevocation_IsRevoked()
    {
        var replacement = Guid.CreateVersion7();

        var token = RefreshToken.Restore(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), "hash", Now, Now.AddDays(1), Now.AddHours(1), replacement);

        token.IsRevoked.ShouldBeTrue();
        token.ReplacedById.ShouldBe(replacement);
    }

    /// <summary>
    /// Missing identifiers, an empty hash or a non-positive lifetime are rejected.
    /// </summary>
    [Fact]
    public void Issue_WithInvalidValues_Throws()
    {
        var id = Guid.CreateVersion7();

        Should.Throw<DomainException>(() => RefreshToken.Issue(Guid.Empty, id, "hash", Now, TimeSpan.FromDays(1)));
        Should.Throw<DomainException>(() => RefreshToken.Issue(id, Guid.Empty, "hash", Now, TimeSpan.FromDays(1)));
        Should.Throw<DomainException>(() => RefreshToken.Issue(id, id, " ", Now, TimeSpan.FromDays(1)));
        Should.Throw<DomainException>(() => RefreshToken.Issue(id, id, "hash", Now, TimeSpan.Zero));
    }
}
