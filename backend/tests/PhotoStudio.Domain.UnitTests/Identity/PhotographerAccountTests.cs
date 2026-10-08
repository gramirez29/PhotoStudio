using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Domain.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="PhotographerAccount"/>.
/// </summary>
public sealed class PhotographerAccountTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A new account stores the email trimmed and in lower case, with no failed logins and no lock.
    /// </summary>
    [Fact]
    public void Create_NormalizesTheEmailAndStartsClean()
    {
        var id = Guid.CreateVersion7();

        var account = PhotographerAccount.Create(id, "  Ana.Photo@Example.COM ", "hash", Now);

        account.Id.ShouldBe(id);
        account.Email.ShouldBe("ana.photo@example.com");
        account.PasswordHash.ShouldBe("hash");
        account.CreatedAt.ShouldBe(Now);
        account.FailedLoginAttempts.ShouldBe(0);
        account.LockedUntil.ShouldBeNull();
        account.Version.ShouldBe(0);
    }

    /// <summary>
    /// An empty identifier or password hash is rejected.
    /// </summary>
    [Fact]
    public void Create_WithMissingValues_Throws()
    {
        Should.Throw<DomainException>(() => PhotographerAccount.Create(Guid.Empty, "ana@example.com", "hash", Now))
            .Code.ShouldBe(DomainErrorCodes.RequiredValue);
        Should.Throw<DomainException>(() => PhotographerAccount.Create(Guid.CreateVersion7(), "ana@example.com", " ", Now))
            .Code.ShouldBe(DomainErrorCodes.RequiredValue);
    }

    /// <summary>
    /// Emails without a valid shape are rejected with a stable code.
    /// </summary>
    /// <param name="email">Invalid email.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ana")]
    [InlineData("@example.com")]
    [InlineData("ana@")]
    [InlineData("ana@example")]
    [InlineData("ana@.com")]
    [InlineData("ana@@example.com")]
    [InlineData("a na@example.com")]
    [InlineData("ana@example.")]
    public void Create_WithInvalidEmail_Throws(string email)
    {
        var exception = Should.Throw<DomainException>(() => PhotographerAccount.Create(Guid.CreateVersion7(), email, "hash", Now));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidEmail);
    }

    /// <summary>
    /// Lookups normalize the email without throwing: an invalid value yields null.
    /// </summary>
    [Fact]
    public void TryNormalizeEmail_ReturnsNullForInvalidValues()
    {
        PhotographerAccount.TryNormalizeEmail(" Ana@Example.com ").ShouldBe("ana@example.com");
        PhotographerAccount.TryNormalizeEmail("not an email").ShouldBeNull();
        PhotographerAccount.TryNormalizeEmail(null).ShouldBeNull();
        PhotographerAccount.TryNormalizeEmail(new string('a', 250) + "@example.com").ShouldBeNull();
    }

    /// <summary>
    /// The account is locked only while the lock end is in the future.
    /// </summary>
    [Fact]
    public void IsLockedAt_IsTrueOnlyBeforeTheLockEnds()
    {
        var lockedUntil = Now.AddMinutes(15);
        var account = PhotographerAccount.Restore(Guid.CreateVersion7(), 3, "ana@example.com", "hash", Now, 0, lockedUntil);

        account.IsLockedAt(Now).ShouldBeTrue();
        account.IsLockedAt(lockedUntil.AddTicks(-1)).ShouldBeTrue();
        account.IsLockedAt(lockedUntil).ShouldBeFalse();
        PhotographerAccount.Create(Guid.CreateVersion7(), "ana@example.com", "hash", Now).IsLockedAt(Now).ShouldBeFalse();
    }

    /// <summary>
    /// The account locks when the failed attempts reach the limit, not before.
    /// </summary>
    [Fact]
    public void ShouldLock_IsTrueFromTheLimitOn()
    {
        PhotographerAccount.ShouldLock(PhotographerAccount.MaxFailedLoginAttempts - 1).ShouldBeFalse();
        PhotographerAccount.ShouldLock(PhotographerAccount.MaxFailedLoginAttempts).ShouldBeTrue();
        PhotographerAccount.ShouldLock(PhotographerAccount.MaxFailedLoginAttempts + 1).ShouldBeTrue();
    }
}
