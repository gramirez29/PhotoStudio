namespace PhotoStudio.Domain.Galleries;

/// <summary>
/// What the browser of the photographer read from the final photo before sending it: its size in pixels and the instant it was
/// taken. It is information for the photographer, never something to decide anything with, so a value that makes no sense is dropped
/// instead of refused: a photo is not rejected because its metadata is odd.
/// </summary>
/// <param name="Width">Width in pixels, or <see langword="null"/> when unknown.</param>
/// <param name="Height">Height in pixels, or <see langword="null"/> when unknown.</param>
/// <param name="CapturedAt">Instant the photo was taken, or <see langword="null"/> when unknown.</param>
public sealed record FinalDetails(int? Width, int? Height, DateTimeOffset? CapturedAt)
{
    /// <summary>The largest side accepted, in pixels; no camera makes a photo bigger.</summary>
    public const int MaxSide = 100_000;

    /// <summary>
    /// Drops what makes no sense: a size needs both sides within range, and a date cannot be before the first digital cameras or in
    /// the future.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns>The details that can be kept.</returns>
    public FinalDetails Normalize(DateTimeOffset now)
    {
        var validSize = Width is > 0 and <= MaxSide && Height is > 0 and <= MaxSide;
        var validDate = CapturedAt is { } taken && taken >= new DateTimeOffset(1990, 1, 1, 0, 0, 0, TimeSpan.Zero) && taken <= now.AddDays(1);
        return new FinalDetails(validSize ? Width : null, validSize ? Height : null, validDate ? CapturedAt : null);
    }
}
