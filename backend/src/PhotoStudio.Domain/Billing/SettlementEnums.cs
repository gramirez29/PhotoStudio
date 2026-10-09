namespace PhotoStudio.Domain.Billing;

/// <summary>
/// Why money has to be given back or kept after a booking ended without being delivered.
/// </summary>
public enum SettlementReason
{
    /// <summary>The photographer cancelled: everything the client paid goes back.</summary>
    PhotographerCancelled,

    /// <summary>The client cancelled a tentative booking: no charge, everything paid goes back.</summary>
    TentativeCancelled,

    /// <summary>The client cancelled a confirmed booking with enough notice: no charge, everything paid goes back.</summary>
    ClientCancelledInTime,

    /// <summary>The client cancelled a confirmed booking too late: the deposit is retained.</summary>
    ClientCancelledLate,

    /// <summary>The tentative hold ended with money already paid: everything paid goes back.</summary>
    BookingExpired,

    /// <summary>The client did not show up: the deposit is retained.</summary>
    ClientAbsent,
}

/// <summary>
/// State of the part of the deposit that the photographer keeps.
/// </summary>
public enum RetentionStatus
{
    /// <summary>Nothing is retained.</summary>
    None,

    /// <summary>The photographer keeps the amount.</summary>
    Applied,

    /// <summary>The retention was undone (for example, the absence mark was reverted).</summary>
    Reversed,
}

/// <summary>
/// State of the money owed back to the client.
/// </summary>
public enum RefundStatus
{
    /// <summary>Nothing is owed back.</summary>
    None,

    /// <summary>The photographer still has to give the money back.</summary>
    Pending,

    /// <summary>The photographer gave the money back and recorded it. Final.</summary>
    Completed,

    /// <summary>It stopped being owed (for example, the absence mark was reverted before paying it).</summary>
    Voided,
}
