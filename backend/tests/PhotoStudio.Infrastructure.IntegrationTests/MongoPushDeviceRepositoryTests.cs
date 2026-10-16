using Microsoft.Extensions.Logging.Abstractions;
using PhotoStudio.Domain.Notifications;
using PhotoStudio.Infrastructure.Persistence;

namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Tests of <see cref="MongoPushDeviceRepository"/> against a real MongoDB replica set.
/// </summary>
/// <param name="fixture">Throwaway database shared by the tests of this class.</param>
public sealed class MongoPushDeviceRepositoryTests(MongoFixture fixture) : IClassFixture<MongoFixture>
{
    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Creates the repository after making sure the indexes exist.
    /// </summary>
    /// <returns>The repository.</returns>
    private async Task<MongoPushDeviceRepository> NewRepositoryAsync()
    {
        await new MongoIndexInitializer(fixture.Database, NullLogger<MongoIndexInitializer>.Instance).StartAsync(TestContext.Current.CancellationToken);
        return new MongoPushDeviceRepository(fixture.Database);
    }

    /// <summary>
    /// Builds a device with a token that is unique to the test.
    /// </summary>
    /// <param name="photographerId">Owner.</param>
    /// <param name="now">Registration instant.</param>
    /// <param name="token">Token, or a new one.</param>
    /// <returns>The device.</returns>
    private static PushDevice Device(Guid photographerId, DateTimeOffset now, string? token = null) =>
        PushDevice.Register(token ?? $"ExponentPushToken[{Guid.NewGuid():N}]", photographerId, PushPlatform.Android, now);

    /// <summary>
    /// A registered device is listed for its photographer and for nobody else.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpsertAsync_StoresTheDeviceForThePhotographer()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var photographer = Guid.CreateVersion7();
        var device = Device(photographer, Now);

        await repository.UpsertAsync(device, TestContext.Current.CancellationToken);

        var listed = await repository.ListByPhotographerAsync(photographer, TestContext.Current.CancellationToken);
        listed.Single().Token.ShouldBe(device.Token);
        listed.Single().Platform.ShouldBe(PushPlatform.Android);
        listed.Single().RegisteredAt.ShouldBe(Now);
        (await repository.ListByPhotographerAsync(Guid.CreateVersion7(), TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    /// <summary>
    /// Registering the same phone again keeps one device, keeps the first registration instant and refreshes the last-seen one.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpsertAsync_WithTheSameToken_KeepsOneDeviceAndRefreshesLastSeen()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var photographer = Guid.CreateVersion7();
        var first = Device(photographer, Now);

        await repository.UpsertAsync(first, TestContext.Current.CancellationToken);
        await repository.UpsertAsync(Device(photographer, Now.AddDays(1), first.Token), TestContext.Current.CancellationToken);

        var listed = await repository.ListByPhotographerAsync(photographer, TestContext.Current.CancellationToken);
        listed.Count.ShouldBe(1);
        listed[0].RegisteredAt.ShouldBe(Now);
        listed[0].LastSeenAt.ShouldBe(Now.AddDays(1));
    }

    /// <summary>
    /// A phone that another photographer signs in on moves to them: the previous owner stops being notified on it.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task UpsertAsync_FromAnotherPhotographer_MovesTheDevice()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var previous = Guid.CreateVersion7();
        var next = Guid.CreateVersion7();
        var device = Device(previous, Now);
        await repository.UpsertAsync(device, TestContext.Current.CancellationToken);

        await repository.UpsertAsync(Device(next, Now.AddHours(1), device.Token), TestContext.Current.CancellationToken);

        (await repository.ListByPhotographerAsync(previous, TestContext.Current.CancellationToken)).ShouldBeEmpty();
        (await repository.ListByPhotographerAsync(next, TestContext.Current.CancellationToken)).Single().Token.ShouldBe(device.Token);
    }

    /// <summary>
    /// A photographer can remove only their own device: a token of someone else is left alone.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task DeleteAsync_RemovesOnlyTheDeviceOfThePhotographer()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var owner = Guid.CreateVersion7();
        var stranger = Guid.CreateVersion7();
        var device = Device(owner, Now);
        await repository.UpsertAsync(device, TestContext.Current.CancellationToken);

        await repository.DeleteAsync(stranger, device.Token, TestContext.Current.CancellationToken);
        (await repository.ListByPhotographerAsync(owner, TestContext.Current.CancellationToken)).Count.ShouldBe(1);

        await repository.DeleteAsync(owner, device.Token, TestContext.Current.CancellationToken);
        (await repository.ListByPhotographerAsync(owner, TestContext.Current.CancellationToken)).ShouldBeEmpty();
    }

    /// <summary>
    /// The tokens the provider reports as invalid are removed whoever owns them; the others stay. An empty list does nothing.
    /// </summary>
    /// <returns>A task that completes when the test finishes.</returns>
    [Fact]
    public async Task DeleteTokensAsync_RemovesTheGivenTokensOnly()
    {
        Assert.SkipUnless(fixture.IsAvailable, "MongoDB is not reachable; run 'docker compose up -d' in the backend folder.");
        var repository = await NewRepositoryAsync();
        var photographer = Guid.CreateVersion7();
        var gone = Device(photographer, Now);
        var kept = Device(photographer, Now);
        await repository.UpsertAsync(gone, TestContext.Current.CancellationToken);
        await repository.UpsertAsync(kept, TestContext.Current.CancellationToken);

        await repository.DeleteTokensAsync([], TestContext.Current.CancellationToken);
        await repository.DeleteTokensAsync([gone.Token], TestContext.Current.CancellationToken);

        (await repository.ListByPhotographerAsync(photographer, TestContext.Current.CancellationToken)).Single().Token.ShouldBe(kept.Token);
    }
}
