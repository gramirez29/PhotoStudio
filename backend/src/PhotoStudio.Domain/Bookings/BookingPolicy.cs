using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Bookings;

/// <summary>
/// Business policy copied into each booking when it is created. Later changes to the photographer's
/// policy never affect existing bookings, which keeps what the client accepted enforceable.
/// </summary>
public sealed record BookingPolicy
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BookingPolicy"/> class. Use the static factory methods, which validate the values.
    /// </summary>
    private BookingPolicy(
        int tentativeHoldHours,
        decimal depositPercentage,
        int freeCancellationWindowHours,
        int rescheduleMinNoticeHours,
        int maxReschedules,
        int noShowToleranceMinutes,
        int noShowRevertWindowDays)
    {
        TentativeHoldHours = tentativeHoldHours;
        DepositPercentage = depositPercentage;
        FreeCancellationWindowHours = freeCancellationWindowHours;
        RescheduleMinNoticeHours = rescheduleMinNoticeHours;
        MaxReschedules = maxReschedules;
        NoShowToleranceMinutes = noShowToleranceMinutes;
        NoShowRevertWindowDays = noShowRevertWindowDays;
    }

    /// <summary>
    /// Gets the default policy proposed in the design workbook.
    /// </summary>
    public static BookingPolicy Default { get; } = new(48, 0.5m, 72, 48, 1, 30, 7);

    /// <summary>Gets how many hours a tentative booking holds the slot before it expires.</summary>
    public int TentativeHoldHours { get; }

    /// <summary>Gets the deposit required to confirm, as a fraction of the package price (0 to 1).</summary>
    public decimal DepositPercentage { get; }

    /// <summary>Gets the minimum notice, in hours before the session, for a cancellation without retention.</summary>
    public int FreeCancellationWindowHours { get; }

    /// <summary>Gets the minimum notice, in hours before the session, for a client-requested reschedule.</summary>
    public int RescheduleMinNoticeHours { get; }

    /// <summary>Gets how many times the client can reschedule the session.</summary>
    public int MaxReschedules { get; }

    /// <summary>Gets the minutes after the session start before the photographer can mark a no-show.</summary>
    public int NoShowToleranceMinutes { get; }

    /// <summary>Gets the days during which a no-show can be reverted.</summary>
    public int NoShowRevertWindowDays { get; }

    /// <summary>
    /// Creates a policy after validating every value.
    /// </summary>
    /// <param name="tentativeHoldHours">Hours a tentative booking holds the slot; greater than zero.</param>
    /// <param name="depositPercentage">Deposit as a fraction of the price, between 0 and 1.</param>
    /// <param name="freeCancellationWindowHours">Hours of notice for a cancellation without retention; zero or greater.</param>
    /// <param name="rescheduleMinNoticeHours">Hours of notice for a client reschedule; zero or greater.</param>
    /// <param name="maxReschedules">Maximum client reschedules; zero or greater.</param>
    /// <param name="noShowToleranceMinutes">Tolerance before a no-show can be marked; zero or greater.</param>
    /// <param name="noShowRevertWindowDays">Days during which a no-show can be reverted; zero or greater.</param>
    /// <returns>The validated policy.</returns>
    /// <exception cref="DomainException">When any value is out of range.</exception>
    public static BookingPolicy Create(
        int tentativeHoldHours,
        decimal depositPercentage,
        int freeCancellationWindowHours,
        int rescheduleMinNoticeHours,
        int maxReschedules,
        int noShowToleranceMinutes,
        int noShowRevertWindowDays)
    {
        if (tentativeHoldHours <= 0)
        {
            throw new DomainException(DomainErrorCodes.InvalidPolicy, "The tentative hold must be at least one hour.");
        }

        if (depositPercentage is < 0m or > 1m)
        {
            throw new DomainException(DomainErrorCodes.InvalidPolicy, "The deposit percentage must be between 0 and 1.");
        }

        if (freeCancellationWindowHours < 0 || rescheduleMinNoticeHours < 0 || maxReschedules < 0
            || noShowToleranceMinutes < 0 || noShowRevertWindowDays < 0)
        {
            throw new DomainException(DomainErrorCodes.InvalidPolicy, "Policy windows and limits cannot be negative.");
        }

        return new BookingPolicy(
            tentativeHoldHours,
            depositPercentage,
            freeCancellationWindowHours,
            rescheduleMinNoticeHours,
            maxReschedules,
            noShowToleranceMinutes,
            noShowRevertWindowDays);
    }
}
