using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// MongoDB data model of a push device (collection <c>push_devices</c>). The Expo push token is the document identifier, so a
/// token exists once in the whole collection without needing a separate unique index. Mapping lives in
/// <see cref="PushDeviceDocumentMappings"/>.
/// </summary>
public sealed class PushDeviceDocument
{
    /// <summary>Gets or sets the Expo push token, which identifies the device.</summary>
    [BsonId]
    public string Token { get; set; } = string.Empty;

    /// <summary>Gets or sets the photographer (tenant) the device belongs to.</summary>
    [BsonElement("photographerId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid PhotographerId { get; set; }

    /// <summary>Gets or sets the platform name.</summary>
    [BsonElement("platform")]
    public string Platform { get; set; } = string.Empty;

    /// <summary>Gets or sets the first registration instant (UTC).</summary>
    [BsonElement("registeredAt")]
    public DateTime RegisteredAt { get; set; }

    /// <summary>Gets or sets the last registration instant (UTC).</summary>
    [BsonElement("lastSeenAt")]
    public DateTime LastSeenAt { get; set; }
}

/// <summary>
/// Mapping between push devices and their MongoDB documents.
/// </summary>
public static class PushDeviceDocumentMappings
{
    /// <summary>
    /// Maps a push device to its document.
    /// </summary>
    /// <param name="device">Device to map.</param>
    /// <returns>The document.</returns>
    public static PushDeviceDocument ToDocument(this PushDevice device)
    {
        ArgumentNullException.ThrowIfNull(device);

        return new PushDeviceDocument
        {
            Token = device.Token,
            PhotographerId = device.PhotographerId,
            Platform = device.Platform.ToString(),
            RegisteredAt = device.RegisteredAt.UtcDateTime,
            LastSeenAt = device.LastSeenAt.UtcDateTime,
        };
    }

    /// <summary>
    /// Maps a push device document to the domain device.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The device.</returns>
    public static PushDevice ToDomain(this PushDeviceDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return PushDevice.Restore(
            document.Token,
            document.PhotographerId,
            Enum.Parse<PushPlatform>(document.Platform),
            ToOffset(document.RegisteredAt),
            ToOffset(document.LastSeenAt));
    }

    /// <summary>
    /// Converts a stored date, which MongoDB returns as UTC, to a UTC offset instant.
    /// </summary>
    /// <param name="value">Stored date.</param>
    /// <returns>The same instant with a zero offset.</returns>
    private static DateTimeOffset ToOffset(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
}
