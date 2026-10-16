using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Notifications;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="IPushDeviceRepository"/> over the <c>push_devices</c> collection. Registering is one
/// atomic upsert keyed by the token, so two registrations of the same phone at once cannot create two documents.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoPushDeviceRepository(IMongoDatabase database) : IPushDeviceRepository
{
    /// <summary>
    /// Name of the push devices collection.
    /// </summary>
    public const string CollectionName = "push_devices";

    private readonly IMongoCollection<PushDeviceDocument> _collection = database.GetCollection<PushDeviceDocument>(CollectionName);

    /// <inheritdoc />
    public async Task UpsertAsync(PushDevice device, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(device);

        var document = device.ToDocument();
        await _collection.UpdateOneAsync(
            candidate => candidate.Token == document.Token,
            Builders<PushDeviceDocument>.Update
                .Set(candidate => candidate.PhotographerId, document.PhotographerId)
                .Set(candidate => candidate.Platform, document.Platform)
                .Set(candidate => candidate.LastSeenAt, document.LastSeenAt)
                .SetOnInsert(candidate => candidate.RegisteredAt, document.RegisteredAt),
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteAsync(Guid photographerId, string token, CancellationToken cancellationToken) =>
        await _collection.DeleteOneAsync(
            candidate => candidate.Token == token && candidate.PhotographerId == photographerId,
            cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PushDevice>> ListByPhotographerAsync(Guid photographerId, CancellationToken cancellationToken)
    {
        // Served by the photographerId index created at startup.
        var documents = await _collection
            .Find(candidate => candidate.PhotographerId == photographerId)
            .ToListAsync(cancellationToken);

        return [.. documents.Select(document => document.ToDomain())];
    }

    /// <inheritdoc />
    public async Task DeleteTokensAsync(IReadOnlyCollection<string> tokens, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tokens);

        if (tokens.Count == 0)
        {
            return;
        }

        await _collection.DeleteManyAsync(candidate => tokens.Contains(candidate.Token), cancellationToken);
    }
}
