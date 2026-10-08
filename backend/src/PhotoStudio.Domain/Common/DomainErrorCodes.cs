namespace PhotoStudio.Domain.Common;

/// <summary>
/// Stable error codes raised by the domain. Clients (mobile app, client portal) branch on these values.
/// </summary>
public static class DomainErrorCodes
{
    /// <summary>A value that must be provided was empty or missing.</summary>
    public const string RequiredValue = "domain.required_value";

    /// <summary>A monetary amount was negative or otherwise invalid.</summary>
    public const string InvalidAmount = "money.invalid_amount";

    /// <summary>The currency code is not a valid three-letter ISO 4217 code.</summary>
    public const string InvalidCurrency = "money.invalid_currency";

    /// <summary>Two amounts with different currencies were combined.</summary>
    public const string CurrencyMismatch = "money.currency_mismatch";

    /// <summary>The end of a time slot is not after its start.</summary>
    public const string InvalidTimeSlot = "schedule.invalid_time_slot";

    /// <summary>A booking policy value is outside its allowed range.</summary>
    public const string InvalidPolicy = "booking.invalid_policy";

    /// <summary>The requested transition is not allowed from the current booking status.</summary>
    public const string InvalidTransition = "booking.invalid_transition";

    /// <summary>The transition is allowed from the current status, but one of its conditions is not met.</summary>
    public const string GuardFailed = "booking.guard_failed";

    /// <summary>The actor is not allowed to perform the requested action.</summary>
    public const string ActorNotAllowed = "booking.actor_not_allowed";

    /// <summary>The session start is not in the future.</summary>
    public const string SessionInPast = "booking.session_in_past";

    /// <summary>The contract of the booking was already signed.</summary>
    public const string ContractAlreadySigned = "booking.contract_already_signed";

    /// <summary>A reason is mandatory for the requested action.</summary>
    public const string ReasonRequired = "booking.reason_required";

    /// <summary>The referenced payment does not exist in the booking.</summary>
    public const string PaymentNotFound = "payment.not_found";

    /// <summary>The payment method is not accepted for the requested operation.</summary>
    public const string InvalidPaymentMethod = "payment.invalid_method";

    /// <summary>The payment is not in a status that allows the requested operation.</summary>
    public const string InvalidPaymentStatus = "payment.invalid_status";
}
