namespace PhotoStudio.Domain.Bookings;

/// <summary>
/// Lifecycle status of a booking. Contract and payment are conditions to reach <see cref="Confirmed"/>, not statuses.
/// </summary>
public enum BookingStatus
{
    /// <summary>Created by the photographer; the slot is held until the booking expires.</summary>
    Tentative,

    /// <summary>Contract signed and deposit verified; the booking policy is binding.</summary>
    Confirmed,

    /// <summary>The session took place. The gallery workflow continues in its own aggregate.</summary>
    Completed,

    /// <summary>Cancelled by the client or the photographer. Final.</summary>
    Cancelled,

    /// <summary>The hold period ended before the booking was confirmed. Final.</summary>
    Expired,

    /// <summary>The client did not show up. Final, but the photographer can revert it within a window.</summary>
    ClientAbsent,
}

/// <summary>
/// Who performs an action on a booking.
/// </summary>
public enum Actor
{
    /// <summary>The photographer's client, acting through the web portal link.</summary>
    Client,

    /// <summary>The photographer, acting through the mobile app.</summary>
    Photographer,

    /// <summary>The platform itself (background jobs and automatic transitions).</summary>
    System,
}

/// <summary>
/// Channel through which an action was performed.
/// </summary>
public enum Channel
{
    /// <summary>The client portal reached through the booking link.</summary>
    Portal,

    /// <summary>The photographer's mobile app, for actions that do not involve the client.</summary>
    PhotographerApp,

    /// <summary>Face to face, captured on the photographer's device.</summary>
    InPerson,

    /// <summary>Outside the platform, for example a paper contract that was photographed and attached.</summary>
    External,
}

/// <summary>
/// Method used to pay.
/// </summary>
public enum PaymentMethod
{
    /// <summary>SINPE Móvil transfer.</summary>
    SinpeMovil,

    /// <summary>Cash handed to the photographer.</summary>
    Cash,

    /// <summary>Card payment through a payment gateway.</summary>
    Card,
}

/// <summary>
/// Verification status of a payment. Only verified payments count toward the balance.
/// </summary>
public enum PaymentStatus
{
    /// <summary>The client uploaded a proof that the photographer has not reviewed yet.</summary>
    PendingVerification,

    /// <summary>The payment was confirmed and counts toward the balance.</summary>
    Verified,

    /// <summary>The proof was rejected and does not count toward the balance.</summary>
    Rejected,
}

/// <summary>
/// Actions that clients can show as buttons. Computed by the domain so the rules live in a single place.
/// </summary>
public enum BookingAction
{
    /// <summary>Sign the booking contract.</summary>
    SignContract,

    /// <summary>Upload a SINPE Móvil proof of payment.</summary>
    SubmitPaymentProof,

    /// <summary>Verify or reject a pending proof of payment.</summary>
    VerifyPayment,

    /// <summary>Record a payment received face to face.</summary>
    RecordInPersonPayment,

    /// <summary>Move the session to another slot.</summary>
    Reschedule,

    /// <summary>Cancel the booking.</summary>
    Cancel,

    /// <summary>Mark the session as completed.</summary>
    Complete,

    /// <summary>Mark that the client did not show up.</summary>
    MarkClientAbsent,

    /// <summary>Undo a client-absent mark made by mistake.</summary>
    RevertClientAbsent,
}
