using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Portal;
using PhotoStudio.Infrastructure.Persistence.Documents;

namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB implementation of <see cref="IPortalOtpRepository"/> over the <c>portal_otps</c> collection. The booking
/// identifier is the document identifier, so a new code replaces the previous one. Guesses are counted with an atomic
/// <c>$inc</c>, so parallel guesses cannot slip past the attempt limit.
/// </summary>
/// <param name="database">MongoDB database.</param>
public sealed class MongoPortalOtpRepository(IMongoDatabase database) : IPortalOtpRepository
{
    /// <summary>
    /// Name of the codes collection.
    /// </summary>
    public const string CollectionName = "portal_otps";

    private readonly IMongoCollection<PortalOtpDocument> _collection = database.GetCollection<PortalOtpDocument>(CollectionName);

    /// <inheritdoc />
    public async Task<PortalOtp?> GetAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var document = await _collection
            .Find(otp => otp.BookingId == bookingId)
            .FirstOrDefaultAsync(cancellationToken);

        return document?.ToDomain();
    }

    /// <inheritdoc />
    public Task SaveAsync(PortalOtp otp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(otp);
        return _collection.ReplaceOneAsync(
            stored => stored.BookingId == otp.BookingId,
            otp.ToDocument(),
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PortalOtp?> RegisterAttemptAsync(Guid bookingId, CancellationToken cancellationToken)
    {
        var updated = await _collection.FindOneAndUpdateAsync(
            otp => otp.BookingId == bookingId,
            Builders<PortalOtpDocument>.Update.Inc(otp => otp.FailedAttempts, 1),
            new FindOneAndUpdateOptions<PortalOtpDocument> { ReturnDocument = ReturnDocument.After },
            cancellationToken);

        return updated?.ToDomain();
    }

    /// <inheritdoc />
    public Task DeleteAsync(Guid bookingId, CancellationToken cancellationToken) =>
        _collection.DeleteOneAsync(otp => otp.BookingId == bookingId, cancellationToken);
}
