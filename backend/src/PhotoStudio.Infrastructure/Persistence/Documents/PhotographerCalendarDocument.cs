using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// Schedule of one photographer: the time ranges taken by active (tentative or confirmed) bookings.
/// It is the single serialization point that prevents double booking: reserving a range is one conditional
/// update on this document, so two concurrent requests can never both pass the overlap check.
/// </summary>
public sealed class PhotographerCalendarDocument
{
    /// <summary>Gets or sets the photographer identifier, which is also the document identifier.</summary>
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    /// <summary>Gets or sets the ranges currently reserved. Ended ranges are pruned whenever a new range is reserved.</summary>
    [BsonElement("entries")]
    public List<CalendarEntryDocument> Entries { get; set; } = [];
}

/// <summary>
/// A half-open time range [Start, End) reserved by one booking.
/// </summary>
public sealed class CalendarEntryDocument
{
    /// <summary>Gets or sets the booking that owns the range.</summary>
    [BsonElement("bookingId")]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid BookingId { get; set; }

    /// <summary>Gets or sets the inclusive start of the range (UTC).</summary>
    [BsonElement("start")]
    public DateTime Start { get; set; }

    /// <summary>Gets or sets the exclusive end of the range (UTC).</summary>
    [BsonElement("end")]
    public DateTime End { get; set; }
}
