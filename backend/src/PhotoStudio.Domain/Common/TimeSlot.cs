namespace PhotoStudio.Domain.Common;

/// <summary>
/// Half-open time interval [Start, End) expressed in UTC.
/// </summary>
public sealed record TimeSlot
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TimeSlot"/> class. Use the static factory methods, which validate the values.
    /// </summary>
    private TimeSlot(DateTimeOffset start, DateTimeOffset end)
    {
        Start = start;
        End = end;
    }

    /// <summary>
    /// Gets the inclusive start of the slot (UTC).
    /// </summary>
    public DateTimeOffset Start { get; }

    /// <summary>
    /// Gets the exclusive end of the slot (UTC).
    /// </summary>
    public DateTimeOffset End { get; }

    /// <summary>
    /// Gets the length of the slot.
    /// </summary>
    public TimeSpan Duration => End - Start;

    /// <summary>
    /// Creates a slot after validating that it ends after it starts.
    /// </summary>
    /// <param name="start">Start of the slot, in any offset.</param>
    /// <param name="end">End of the slot, in any offset.</param>
    /// <returns>A slot normalized to UTC.</returns>
    /// <exception cref="DomainException">When <paramref name="end"/> is not after <paramref name="start"/>.</exception>
    public static TimeSlot Create(DateTimeOffset start, DateTimeOffset end)
    {
        if (end <= start)
        {
            throw new DomainException(DomainErrorCodes.InvalidTimeSlot, "The end of the slot must be after its start.");
        }

        return new TimeSlot(start.ToUniversalTime(), end.ToUniversalTime());
    }

    /// <summary>
    /// Indicates whether this slot overlaps another one.
    /// </summary>
    /// <param name="other">Slot to compare with.</param>
    /// <returns><see langword="true"/> when both slots share any instant.</returns>
    public bool Overlaps(TimeSlot other) => Start < other.End && other.Start < End;
}
