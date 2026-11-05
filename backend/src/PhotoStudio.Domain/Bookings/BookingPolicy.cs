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
        int clientAbsentToleranceMinutes,
        int clientAbsentRevertWindowDays,
        decimal lateCancellationRetention,
        decimal clientAbsentRetention,
        decimal photographerCancellationRefund,
        int deliveryDeadlineDays)
    {
        TentativeHoldHours = tentativeHoldHours;
        DepositPercentage = depositPercentage;
        FreeCancellationWindowHours = freeCancellationWindowHours;
        RescheduleMinNoticeHours = rescheduleMinNoticeHours;
        MaxReschedules = maxReschedules;
        ClientAbsentToleranceMinutes = clientAbsentToleranceMinutes;
        ClientAbsentRevertWindowDays = clientAbsentRevertWindowDays;
        LateCancellationRetention = lateCancellationRetention;
        ClientAbsentRetention = clientAbsentRetention;
        PhotographerCancellationRefund = photographerCancellationRefund;
        DeliveryDeadlineDays = deliveryDeadlineDays;
    }

    /// <summary>Days to deliver the final photos when the policy does not say.</summary>
    public const int DefaultDeliveryDeadlineDays = 21;

    /// <summary>Longest delivery deadline accepted.</summary>
    public const int MaxDeliveryDeadlineDays = 365;

    /// <summary>
    /// Gets the default policy proposed in the design workbook.
    /// </summary>
    public static BookingPolicy Default { get; } = new(48, 0.5m, 72, 48, 1, 30, 7, 1m, 1m, 1m, 21);

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

    /// <summary>Gets the minutes after the session start before the photographer can mark the client absent.</summary>
    public int ClientAbsentToleranceMinutes { get; }

    /// <summary>Gets the days during which a client-absent mark can be reverted.</summary>
    public int ClientAbsentRevertWindowDays { get; }

    /// <summary>Gets the share of the deposit the photographer keeps when the client cancels a confirmed booking with less notice than <see cref="FreeCancellationWindowHours"/> (0 to 1).</summary>
    public decimal LateCancellationRetention { get; }

    /// <summary>Gets the share of the deposit the photographer keeps when the client does not show up (0 to 1).</summary>
    public decimal ClientAbsentRetention { get; }

    /// <summary>Gets the share of what the client paid that goes back when the photographer cancels (0 to 1).</summary>
    public decimal PhotographerCancellationRefund { get; }

    /// <summary>Gets the days, counted from the moment the client sends the selection, within which the photographer should publish the final photos.</summary>
    public int DeliveryDeadlineDays { get; }

    /// <summary>
    /// Creates a policy after validating every value.
    /// </summary>
    /// <param name="tentativeHoldHours">Hours a tentative booking holds the slot; greater than zero.</param>
    /// <param name="depositPercentage">Deposit as a fraction of the price, between 0 and 1.</param>
    /// <param name="freeCancellationWindowHours">Hours of notice for a cancellation without retention; zero or greater.</param>
    /// <param name="rescheduleMinNoticeHours">Hours of notice for a client reschedule; zero or greater.</param>
    /// <param name="maxReschedules">Maximum client reschedules; zero or greater.</param>
    /// <param name="clientAbsentToleranceMinutes">Tolerance before the client can be marked absent; zero or greater.</param>
    /// <param name="clientAbsentRevertWindowDays">Days during which a client-absent mark can be reverted; zero or greater.</param>
    /// <param name="lateCancellationRetention">Share of the deposit kept when the client cancels late, between 0 and 1.</param>
    /// <param name="clientAbsentRetention">Share of the deposit kept when the client does not show up, between 0 and 1.</param>
    /// <param name="photographerCancellationRefund">Share of what was paid that goes back when the photographer cancels, between 0 and 1.</param>
    /// <param name="deliveryDeadlineDays">Days to deliver the final photos after the selection, between 1 and <see cref="MaxDeliveryDeadlineDays"/>.</param>
    /// <returns>The validated policy.</returns>
    /// <exception cref="DomainException">When any value is out of range.</exception>
    public static BookingPolicy Create(
        int tentativeHoldHours,
        decimal depositPercentage,
        int freeCancellationWindowHours,
        int rescheduleMinNoticeHours,
        int maxReschedules,
        int clientAbsentToleranceMinutes,
        int clientAbsentRevertWindowDays,
        decimal lateCancellationRetention = 1m,
        decimal clientAbsentRetention = 1m,
        decimal photographerCancellationRefund = 1m,
        int deliveryDeadlineDays = DefaultDeliveryDeadlineDays)
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
            || clientAbsentToleranceMinutes < 0 || clientAbsentRevertWindowDays < 0)
        {
            throw new DomainException(DomainErrorCodes.InvalidPolicy, "Policy windows and limits cannot be negative.");
        }

        if (lateCancellationRetention is < 0m or > 1m || clientAbsentRetention is < 0m or > 1m)
        {
            throw new DomainException(DomainErrorCodes.InvalidPolicy, "The share of the deposit that is kept must be between 0 and 1.");
        }

        if (photographerCancellationRefund is < 0m or > 1m)
        {
            throw new DomainException(DomainErrorCodes.InvalidPolicy, "The share refunded when the photographer cancels must be between 0 and 1.");
        }

        if (deliveryDeadlineDays is < 1 or > MaxDeliveryDeadlineDays)
        {
            throw new DomainException(DomainErrorCodes.InvalidPolicy, $"The delivery deadline must be between 1 and {MaxDeliveryDeadlineDays} days.");
        }

        return new BookingPolicy(
            tentativeHoldHours,
            depositPercentage,
            freeCancellationWindowHours,
            rescheduleMinNoticeHours,
            maxReschedules,
            clientAbsentToleranceMinutes,
            clientAbsentRevertWindowDays,
            lateCancellationRetention,
            clientAbsentRetention,
            photographerCancellationRefund,
            deliveryDeadlineDays);
    }
}
