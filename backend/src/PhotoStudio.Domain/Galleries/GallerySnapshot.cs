namespace PhotoStudio.Domain.Galleries;

/// <summary>
/// Who the gallery is for and which session it comes from, copied when the gallery is created so the list of galleries shows
/// it without reading the booking.
/// </summary>
/// <param name="ClientName">Name of the client.</param>
/// <param name="PackageName">Name of the booked package.</param>
/// <param name="SessionStart">Start of the session (UTC).</param>
public sealed record GallerySnapshot(string ClientName, string PackageName, DateTimeOffset SessionStart);
