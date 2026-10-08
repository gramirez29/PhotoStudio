using System.Globalization;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Infrastructure.Policies;

/// <summary>
/// Reads the booking policy from environment variables, falling back to <see cref="BookingPolicy.Default"/>.
/// Temporary until each photographer can configure their own policy from the app.
/// </summary>
public sealed class EnvironmentBookingPolicyProvider : IBookingPolicyProvider
{
    private readonly BookingPolicy _policy;

    /// <summary>
    /// Initializes a new instance of the <see cref="EnvironmentBookingPolicyProvider"/> class and reads the variables once.
    /// </summary>
    public EnvironmentBookingPolicyProvider()
    {
        var defaults = BookingPolicy.Default;
        _policy = BookingPolicy.Create(
            ReadInt("BOOKING_TENTATIVE_HOLD_HOURS", defaults.TentativeHoldHours),
            ReadDecimal("BOOKING_DEPOSIT_PERCENTAGE", defaults.DepositPercentage),
            ReadInt("BOOKING_FREE_CANCELLATION_WINDOW_HOURS", defaults.FreeCancellationWindowHours),
            ReadInt("BOOKING_RESCHEDULE_MIN_NOTICE_HOURS", defaults.RescheduleMinNoticeHours),
            ReadInt("BOOKING_MAX_RESCHEDULES", defaults.MaxReschedules),
            ReadInt("BOOKING_CLIENT_ABSENT_TOLERANCE_MINUTES", defaults.ClientAbsentToleranceMinutes),
            ReadInt("BOOKING_CLIENT_ABSENT_REVERT_WINDOW_DAYS", defaults.ClientAbsentRevertWindowDays));
    }

    /// <inheritdoc />
    public Task<BookingPolicy> GetPolicyAsync(Guid photographerId, CancellationToken cancellationToken) => Task.FromResult(_policy);

    /// <summary>
    /// Reads an integer variable.
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="fallback">Value used when the variable is missing.</param>
    /// <returns>The parsed value or the fallback.</returns>
    /// <exception cref="InvalidOperationException">When the variable is present but not an integer.</exception>
    private static int ReadInt(string name, int fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"The environment variable '{name}' must be an integer.");
    }

    /// <summary>
    /// Reads a decimal variable using the invariant culture (dot as decimal separator).
    /// </summary>
    /// <param name="name">Variable name.</param>
    /// <param name="fallback">Value used when the variable is missing.</param>
    /// <returns>The parsed value or the fallback.</returns>
    /// <exception cref="InvalidOperationException">When the variable is present but not a number.</exception>
    private static decimal ReadDecimal(string name, decimal fallback)
    {
        var raw = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return fallback;
        }

        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)
            ? value
            : throw new InvalidOperationException($"The environment variable '{name}' must be a number.");
    }
}
