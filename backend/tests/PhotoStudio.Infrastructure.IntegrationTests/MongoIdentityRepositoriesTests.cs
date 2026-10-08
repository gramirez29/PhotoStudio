using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Identity;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="MongoUserRepository"/> and <see cref="MongoRefreshTokenRepository"/> against a real
/// MongoDB replica set, focused on what must hold under concurrency: the username is unique, the failed-login counter never
/// loses an increment, and a refresh token can be rotated exactly once.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoIdentityRepositoriesTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A user is stored and found both by username and by identifier, with all its data.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task User_IsStoredAndFoundByUsernameAndById()
    {
        await EnsureIndexesAsync();
        var users = NewUsers();
        var user = NewUser();

        await users.AddAsync(user, TestContext.Current.CancellationToken);

        var byUsername = await users.GetByUsernameAsync(user.Username, TestContext.Current.CancellationToken);
        var byId = await users.GetByIdAsync(user.Id, TestContext.Current.CancellationToken);
        byUsername.ShouldNotBeNull();
        byUsername.Id.ShouldBe(user.Id);
        byUsername.PasswordHash.ShouldBe("stored-hash");
        byUsername.CreatedAt.ShouldBe(Now);
        byUsername.FailedLoginAttempts.ShouldBe(0);
        byUsername.LockedUntil.ShouldBeNull();
        byUsername.Name.ShouldBe("Ana Pérez");
        byUsername.Email.ShouldBe(user.Email);
        byUsername.Phone.ShouldBe("+50670189220");
        byId.ShouldNotBeNull();
        byId.Username.ShouldBe(user.Username);
        (await users.GetByUsernameAsync("nobody", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// A second user with the same username is refused by the unique index, whatever the identifier.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AddAsync_WithADuplicateUsername_ThrowsConflict()
    {
        await EnsureIndexesAsync();
        var users = NewUsers();
        var first = NewUser();
        await users.AddAsync(first, TestContext.Current.CancellationToken);
        var second = User.Create(Guid.CreateVersion7(), first.Username, $"{Guid.NewGuid():N}@example.com", "another-hash", "Other", "70189221", Now);

        var exception = await Should.ThrowAsync<ConflictException>(() => users.AddAsync(second, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.UsernameTaken);
    }

    /// <summary>
    /// A second user with the same email is refused with its own code, even when the username is different, and the check
    /// ignores the case the email was typed in (it is stored normalized).
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task AddAsync_WithADuplicateEmail_ThrowsEmailTaken()
    {
        await EnsureIndexesAsync();
        var users = NewUsers();
        var first = NewUser();
        await users.AddAsync(first, TestContext.Current.CancellationToken);
        var second = User.Create(Guid.CreateVersion7(), $"u{Guid.NewGuid():N}"[..13], first.Email.ToUpperInvariant(), "another-hash", "Other", "70189221", Now);

        var exception = await Should.ThrowAsync<ConflictException>(() => users.AddAsync(second, TestContext.Current.CancellationToken));

        exception.Code.ShouldBe(ApplicationErrorCodes.EmailTaken);
    }

    /// <summary>
    /// Users created before the email existed have none. They must not collide with each other, and the unique index must
    /// still be creatable on a collection that already holds them.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UsersWithoutAnEmail_DoNotCollideWithEachOther()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var collection = fixture.Database.GetCollection<BsonDocument>(MongoUserRepository.CollectionName);
        await collection.InsertManyAsync(
            [
                new BsonDocument { { "_id", new BsonBinaryData(Guid.CreateVersion7(), GuidRepresentation.Standard) }, { "username", $"legacy{Guid.NewGuid():N}"[..14] }, { "passwordHash", "x" } },
                new BsonDocument { { "_id", new BsonBinaryData(Guid.CreateVersion7(), GuidRepresentation.Standard) }, { "username", $"legacy{Guid.NewGuid():N}"[..14] }, { "passwordHash", "x" } },
            ],
            cancellationToken: TestContext.Current.CancellationToken);

        await new MongoIndexInitializer(fixture.Database, NullLogger<MongoIndexInitializer>.Instance).StartAsync(TestContext.Current.CancellationToken);

        var listing = await fixture.Database.RunCommandAsync<BsonDocument>(
            new BsonDocument("listIndexes", MongoUserRepository.CollectionName),
            cancellationToken: TestContext.Current.CancellationToken);
        var names = listing["cursor"]["firstBatch"].AsBsonArray.Select(index => index["name"].AsString).ToList();
        names.ShouldContain(MongoUserRepository.EmailIndexName);
        names.ShouldContain(MongoUserRepository.UsernameIndexName);
    }

    /// <summary>
    /// Parallel failed logins are all counted. With load-modify-save and optimistic concurrency most of them would be lost
    /// and an attacker could guess without ever reaching the limit.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RegisterFailedLoginAsync_CountsEveryParallelAttempt()
    {
        await EnsureIndexesAsync();
        var users = NewUsers();
        var user = NewUser();
        await users.AddAsync(user, TestContext.Current.CancellationToken);

        var counts = await Task.WhenAll(
            Enumerable.Range(0, 25).Select(_ => NewUsers().RegisterFailedLoginAsync(user.Id, TestContext.Current.CancellationToken)));

        counts.Order().ShouldBe(Enumerable.Range(1, 25));
        (await users.GetByIdAsync(user.Id, TestContext.Current.CancellationToken))!.FailedLoginAttempts.ShouldBe(25);
    }

    /// <summary>
    /// Locking stores the lock end and restarts the counter; resetting clears both.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task LockAsync_StoresTheLockAndResetClearsIt()
    {
        await EnsureIndexesAsync();
        var users = NewUsers();
        var user = NewUser();
        await users.AddAsync(user, TestContext.Current.CancellationToken);
        await users.RegisterFailedLoginAsync(user.Id, TestContext.Current.CancellationToken);

        await users.LockAsync(user.Id, Now.AddMinutes(15), TestContext.Current.CancellationToken);
        var locked = await users.GetByIdAsync(user.Id, TestContext.Current.CancellationToken);

        locked!.LockedUntil.ShouldBe(Now.AddMinutes(15));
        locked.FailedLoginAttempts.ShouldBe(0);
        locked.IsLockedAt(Now).ShouldBeTrue();

        await users.ResetFailedLoginsAsync(user.Id, TestContext.Current.CancellationToken);
        var cleared = await users.GetByIdAsync(user.Id, TestContext.Current.CancellationToken);

        cleared!.LockedUntil.ShouldBeNull();
        cleared.FailedLoginAttempts.ShouldBe(0);
    }

    /// <summary>
    /// A token is found by the hash of its secret, with all its data.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RefreshToken_IsStoredAndFoundByHash()
    {
        await EnsureIndexesAsync();
        var tokens = NewTokens();
        var token = NewToken();

        await tokens.AddAsync(token, TestContext.Current.CancellationToken);

        var stored = await tokens.GetByHashAsync(token.TokenHash, TestContext.Current.CancellationToken);
        stored.ShouldNotBeNull();
        stored.Id.ShouldBe(token.Id);
        stored.PhotographerId.ShouldBe(token.PhotographerId);
        stored.FamilyId.ShouldBe(token.FamilyId);
        stored.ExpiresAt.ShouldBe(token.ExpiresAt);
        stored.IsRevoked.ShouldBeFalse();
        (await tokens.GetByHashAsync("unknown", TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// Rotation revokes the used token, links it to its replacement and stores the replacement.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RotateAsync_RevokesTheUsedTokenAndStoresTheReplacement()
    {
        await EnsureIndexesAsync();
        var tokens = NewTokens();
        var current = NewToken();
        await tokens.AddAsync(current, TestContext.Current.CancellationToken);
        var replacement = RefreshToken.Issue(current.PhotographerId, current.FamilyId, Hash(), Now, TimeSpan.FromDays(30));

        var rotated = await tokens.RotateAsync(current, replacement, Now.AddMinutes(5), TestContext.Current.CancellationToken);

        rotated.ShouldBeTrue();
        var used = await tokens.GetByHashAsync(current.TokenHash, TestContext.Current.CancellationToken);
        used!.IsRevoked.ShouldBeTrue();
        used.RevokedAt.ShouldBe(Now.AddMinutes(5));
        used.ReplacedById.ShouldBe(replacement.Id);
        var stored = await tokens.GetByHashAsync(replacement.TokenHash, TestContext.Current.CancellationToken);
        stored!.IsRevoked.ShouldBeFalse();
        stored.FamilyId.ShouldBe(current.FamilyId);
    }

    /// <summary>
    /// Rotating a token that was already used fails and stores no second replacement.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RotateAsync_WithAnAlreadyUsedToken_FailsAndStoresNothing()
    {
        await EnsureIndexesAsync();
        var tokens = NewTokens();
        var current = NewToken();
        await tokens.AddAsync(current, TestContext.Current.CancellationToken);
        var first = RefreshToken.Issue(current.PhotographerId, current.FamilyId, Hash(), Now, TimeSpan.FromDays(30));
        var second = RefreshToken.Issue(current.PhotographerId, current.FamilyId, Hash(), Now, TimeSpan.FromDays(30));
        await tokens.RotateAsync(current, first, Now, TestContext.Current.CancellationToken);

        var rotatedAgain = await tokens.RotateAsync(current, second, Now, TestContext.Current.CancellationToken);

        rotatedAgain.ShouldBeFalse();
        (await tokens.GetByHashAsync(second.TokenHash, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    /// <summary>
    /// Of many concurrent rotations of the same token, exactly one succeeds and exactly one replacement is stored.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RotateAsync_WhenManyRequestsRaceForTheSameToken_ExactlyOneWins()
    {
        await EnsureIndexesAsync();
        var current = NewToken();
        await NewTokens().AddAsync(current, TestContext.Current.CancellationToken);
        var replacements = Enumerable.Range(0, 8)
            .Select(_ => RefreshToken.Issue(current.PhotographerId, current.FamilyId, Hash(), Now, TimeSpan.FromDays(30)))
            .ToList();

        var results = await Task.WhenAll(replacements.Select(async replacement =>
        {
            await Task.Yield();
            return await NewTokens().RotateAsync(current, replacement, Now, TestContext.Current.CancellationToken);
        }));

        results.Count(won => won).ShouldBe(1);
        var stored = 0;
        foreach (var replacement in replacements)
        {
            if (await NewTokens().GetByHashAsync(replacement.TokenHash, TestContext.Current.CancellationToken) is not null)
            {
                stored++;
            }
        }

        stored.ShouldBe(1);
    }

    /// <summary>
    /// Revoking a family revokes only the tokens of that family.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task RevokeFamilyAsync_RevokesOnlyThatFamily()
    {
        await EnsureIndexesAsync();
        var tokens = NewTokens();
        var photographerId = Guid.CreateVersion7();
        var familyId = Guid.CreateVersion7();
        var inFamily = RefreshToken.Issue(photographerId, familyId, Hash(), Now, TimeSpan.FromDays(30));
        var otherFamily = RefreshToken.Issue(photographerId, Guid.CreateVersion7(), Hash(), Now, TimeSpan.FromDays(30));
        await tokens.AddAsync(inFamily, TestContext.Current.CancellationToken);
        await tokens.AddAsync(otherFamily, TestContext.Current.CancellationToken);

        var revoked = await tokens.RevokeFamilyAsync(familyId, Now, TestContext.Current.CancellationToken);

        revoked.ShouldBe(1);
        (await tokens.GetByHashAsync(inFamily.TokenHash, TestContext.Current.CancellationToken))!.IsRevoked.ShouldBeTrue();
        (await tokens.GetByHashAsync(otherFamily.TokenHash, TestContext.Current.CancellationToken))!.IsRevoked.ShouldBeFalse();
    }

    /// <summary>
    /// Creates the indexes the repositories rely on (the unique username and token hash), skipping the test when MongoDB is down.
    /// </summary>
    /// <returns>A task that completes when the indexes exist.</returns>
    private async Task EnsureIndexesAsync()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        await new MongoIndexInitializer(fixture.Database, NullLogger<MongoIndexInitializer>.Instance).StartAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Creates an account repository over the throwaway database.
    /// </summary>
    /// <returns>A new repository.</returns>
    private MongoUserRepository NewUsers() => new(fixture.Database);

    /// <summary>
    /// Creates a refresh token repository over the throwaway database.
    /// </summary>
    /// <returns>A new repository.</returns>
    private MongoRefreshTokenRepository NewTokens() => new(fixture.Database);

    /// <summary>
    /// Builds a user with a unique username.
    /// </summary>
    /// <returns>The user.</returns>
    private static User NewUser() =>
        User.Create(Guid.CreateVersion7(), $"u{Guid.NewGuid():N}"[..13], $"{Guid.NewGuid():N}@example.com", "stored-hash", "Ana Pérez", "+50670189220", Now);

    /// <summary>
    /// Builds an unused refresh token for a new photographer and a new family.
    /// </summary>
    /// <returns>The token.</returns>
    private static RefreshToken NewToken() =>
        RefreshToken.Issue(Guid.CreateVersion7(), Guid.CreateVersion7(), Hash(), Now, TimeSpan.FromDays(30));

    /// <summary>
    /// Builds a unique token hash.
    /// </summary>
    /// <returns>The hash.</returns>
    private static string Hash() => Guid.NewGuid().ToString("N");
}
