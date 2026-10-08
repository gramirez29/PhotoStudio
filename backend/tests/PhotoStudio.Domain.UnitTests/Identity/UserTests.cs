using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Domain.UnitTests.Identity;

/// <summary>
/// Tests of <see cref="User"/>.
/// </summary>
public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A new user stores the username in lower case, trims the name and the phone, and starts with no failures and no lock.
    /// </summary>
    [Fact]
    public void Create_NormalizesTheDataAndStartsClean()
    {
        var id = Guid.CreateVersion7();

        var user = User.Create(id, "  Ana.Photo_1 ", "  Ana@Example.COM ", "hash", "  Ana Pérez ", " 7018-9220 ", Now);

        user.Id.ShouldBe(id);
        user.Username.ShouldBe("ana.photo_1");
        user.Email.ShouldBe("ana@example.com");
        user.PasswordHash.ShouldBe("hash");
        user.Name.ShouldBe("Ana Pérez");
        user.Phone.ShouldBe("70189220");
        user.CreatedAt.ShouldBe(Now);
        user.FailedLoginAttempts.ShouldBe(0);
        user.LockedUntil.ShouldBeNull();
        user.Version.ShouldBe(0);
    }

    /// <summary>
    /// An empty identifier or password hash is rejected.
    /// </summary>
    [Fact]
    public void Create_WithMissingValues_Throws()
    {
        Should.Throw<DomainException>(() => User.Create(Guid.Empty, "ana", "ana@example.com", "hash", "Ana", "70189220", Now))
            .Code.ShouldBe(DomainErrorCodes.RequiredValue);
        Should.Throw<DomainException>(() => User.Create(Guid.CreateVersion7(), "ana", "ana@example.com", " ", "Ana", "70189220", Now))
            .Code.ShouldBe(DomainErrorCodes.RequiredValue);
    }

    /// <summary>
    /// Usernames that are too short, too long, start or end with a symbol, or use other characters are rejected.
    /// </summary>
    /// <param name="username">Invalid username.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("a b c")]
    [InlineData("ana@example.com")]
    [InlineData(".ana")]
    [InlineData("ana.")]
    [InlineData("-ana")]
    [InlineData("ana_")]
    [InlineData("ánä")]
    [InlineData("ana!")]
    [InlineData("0123456789012345678901234567890")]
    public void Create_WithInvalidUsername_Throws(string username)
    {
        var exception = Should.Throw<DomainException>(() => User.Create(Guid.CreateVersion7(), username, "ana@example.com", "hash", "Ana", "70189220", Now));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidUsername);
    }

    /// <summary>
    /// Usernames at the limits of the rules are accepted.
    /// </summary>
    /// <param name="username">Valid username.</param>
    [Theory]
    [InlineData("abc")]
    [InlineData("a.b-c_d")]
    [InlineData("007")]
    [InlineData("012345678901234567890123456789")]
    public void NormalizeUsername_AcceptsTheValidShapes(string username)
    {
        User.NormalizeUsername(username).ShouldBe(username);
    }

    /// <summary>
    /// Lookups normalize the username without throwing: an invalid value yields null.
    /// </summary>
    [Fact]
    public void TryNormalizeUsername_ReturnsNullForInvalidValues()
    {
        User.TryNormalizeUsername(" Ana ").ShouldBe("ana");
        User.TryNormalizeUsername("not valid!").ShouldBeNull();
        User.TryNormalizeUsername(null).ShouldBeNull();
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
        var exception = Should.Throw<DomainException>(() => User.Create(Guid.CreateVersion7(), "ana", email, "hash", "Ana", "70189220", Now));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidEmail);
    }

    /// <summary>
    /// Emails are trimmed and lower-cased, and the length limit is enforced.
    /// </summary>
    [Fact]
    public void NormalizeEmail_TrimsLowersAndEnforcesTheLimit()
    {
        User.NormalizeEmail("  Ana.Photo+Work@Example.COM ").ShouldBe("ana.photo+work@example.com");
        User.TryNormalizeEmail(null).ShouldBeNull();
        User.TryNormalizeEmail(new string('a', User.MaxEmailLength - 12) + "@example.com").ShouldNotBeNull();
        User.TryNormalizeEmail(new string('a', User.MaxEmailLength - 11) + "@example.com").ShouldBeNull();
    }

    /// <summary>
    /// An empty name or one longer than the limit is rejected.
    /// </summary>
    [Fact]
    public void Create_WithInvalidName_Throws()
    {
        Should.Throw<DomainException>(() => User.Create(Guid.CreateVersion7(), "ana", "ana@example.com", "hash", "   ", "70189220", Now))
            .Code.ShouldBe(DomainErrorCodes.InvalidName);
        Should.Throw<DomainException>(() => User.Create(Guid.CreateVersion7(), "ana", "ana@example.com", "hash", new string('x', User.MaxNameLength + 1), "70189220", Now))
            .Code.ShouldBe(DomainErrorCodes.InvalidName);
        User.NormalizeName(new string('x', User.MaxNameLength)).Length.ShouldBe(User.MaxNameLength);
    }

    /// <summary>
    /// Phones lose their separators and keep an optional leading plus sign.
    /// </summary>
    /// <param name="typed">Phone as typed.</param>
    /// <param name="expected">Phone as stored.</param>
    [Theory]
    [InlineData("7018-9220", "70189220")]
    [InlineData("+506 7018-9220", "+50670189220")]
    [InlineData("(506) 7018.9220", "50670189220")]
    [InlineData("  +50670189220  ", "+50670189220")]
    [InlineData("123456789012345", "123456789012345")]
    public void NormalizePhone_RemovesSeparators(string typed, string expected)
    {
        User.NormalizePhone(typed).ShouldBe(expected);
    }

    /// <summary>
    /// Phones with letters, too few or too many digits, or a misplaced plus sign are rejected.
    /// </summary>
    /// <param name="phone">Invalid phone.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567")]
    [InlineData("1234567890123456")]
    [InlineData("7018-92ab")]
    [InlineData("+")]
    [InlineData("7018+9220")]
    [InlineData("++50670189220")]
    public void NormalizePhone_WithInvalidValues_Throws(string phone)
    {
        var exception = Should.Throw<DomainException>(() => User.NormalizePhone(phone));

        exception.Code.ShouldBe(DomainErrorCodes.InvalidPhone);
    }

    /// <summary>
    /// Passwords shorter than the minimum or longer than the maximum are rejected; the limits themselves are accepted.
    /// </summary>
    [Fact]
    public void ValidatePassword_EnforcesTheLengthLimits()
    {
        Should.Throw<DomainException>(() => User.ValidatePassword(new string('x', User.MinPasswordLength - 1))).Code.ShouldBe(DomainErrorCodes.WeakPassword);
        Should.Throw<DomainException>(() => User.ValidatePassword(new string('x', User.MaxPasswordLength + 1))).Code.ShouldBe(DomainErrorCodes.WeakPassword);
        Should.Throw<DomainException>(() => User.ValidatePassword(null)).Code.ShouldBe(DomainErrorCodes.WeakPassword);
        Should.NotThrow(() => User.ValidatePassword(new string('x', User.MinPasswordLength)));
        Should.NotThrow(() => User.ValidatePassword(new string('x', User.MaxPasswordLength)));
    }

    /// <summary>
    /// The user is locked only while the lock end is in the future.
    /// </summary>
    [Fact]
    public void IsLockedAt_IsTrueOnlyBeforeTheLockEnds()
    {
        var lockedUntil = Now.AddMinutes(15);
        var user = User.Restore(Guid.CreateVersion7(), 3, "ana", "ana@example.com", "hash", "Ana", "70189220", Now, 0, lockedUntil);

        user.IsLockedAt(Now).ShouldBeTrue();
        user.IsLockedAt(lockedUntil.AddTicks(-1)).ShouldBeTrue();
        user.IsLockedAt(lockedUntil).ShouldBeFalse();
        User.Create(Guid.CreateVersion7(), "ana", "ana@example.com", "hash", "Ana", "70189220", Now).IsLockedAt(Now).ShouldBeFalse();
    }

    /// <summary>
    /// The user locks when the failed attempts reach the limit, not before.
    /// </summary>
    [Fact]
    public void ShouldLock_IsTrueFromTheLimitOn()
    {
        User.ShouldLock(User.MaxFailedLoginAttempts - 1).ShouldBeFalse();
        User.ShouldLock(User.MaxFailedLoginAttempts).ShouldBeTrue();
        User.ShouldLock(User.MaxFailedLoginAttempts + 1).ShouldBeTrue();
    }
}
