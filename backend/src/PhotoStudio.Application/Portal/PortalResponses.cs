using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Portal;

/// <summary>
/// Payment as the client sees it in the portal: amount, state and date. The reference and the way it was recorded stay with the photographer.
/// </summary>
/// <param name="Amount">Amount of the payment.</param>
/// <param name="Status">Verification status.</param>
/// <param name="RecordedAt">Instant the payment was recorded (UTC).</param>
public sealed record PortalPaymentResponse(MoneyResponse Amount, PaymentStatus Status, DateTimeOffset RecordedAt);

/// <summary>
/// Booking as the client sees it through the link. It leaves out everything the client does not need and the link must not
/// expose: identifiers, the client's own phone and email, the policy and the history.
/// </summary>
/// <param name="PhotographerName">Name of the photographer, to show whose booking it is.</param>
/// <param name="ClientName">Name of the client.</param>
/// <param name="PackageName">Package name.</param>
/// <param name="Status">Current status.</param>
/// <param name="SessionStart">Session start (UTC).</param>
/// <param name="SessionEnd">Session end (UTC).</param>
/// <param name="ExpiresAt">End of the tentative hold, if any (UTC).</param>
/// <param name="PackagePrice">Package price.</param>
/// <param name="DepositRequired">Deposit required to confirm.</param>
/// <param name="TotalPaid">Sum of verified payments.</param>
/// <param name="Balance">Outstanding balance.</param>
/// <param name="ContractSigned">Whether the contract is signed.</param>
/// <param name="Payments">Payments recorded, with their state.</param>
/// <param name="CanSubmitProof">Whether the client can submit a payment proof now.</param>
/// <param name="CanReceiveCode">Whether the booking has an email to send verification codes to; without it the client cannot act through the link.</param>
public sealed record PortalBookingResponse(
    string PhotographerName,
    string ClientName,
    string PackageName,
    BookingStatus Status,
    DateTimeOffset SessionStart,
    DateTimeOffset SessionEnd,
    DateTimeOffset? ExpiresAt,
    MoneyResponse PackagePrice,
    MoneyResponse DepositRequired,
    MoneyResponse TotalPaid,
    MoneyResponse Balance,
    bool ContractSigned,
    IReadOnlyList<PortalPaymentResponse> Payments,
    bool CanSubmitProof,
    bool CanReceiveCode);

/// <summary>
/// A booking link just created. The token is shown only here: the server keeps its hash, so it cannot be recovered later.
/// </summary>
/// <param name="Token">Secret token of the link.</param>
/// <param name="Url">Full URL to send to the client.</param>
/// <param name="ExpiresAt">Instant the link stops working (UTC).</param>
public sealed record BookingLinkResponse(string Token, string Url, DateTimeOffset ExpiresAt);

/// <summary>
/// Confirmation that a verification code was sent.
/// </summary>
/// <param name="MaskedEmail">Address the code went to, partly hidden, so the client knows where to look.</param>
/// <param name="ExpiresAt">Instant the code stops being accepted (UTC).</param>
public sealed record PortalCodeSentResponse(string MaskedEmail, DateTimeOffset ExpiresAt);

/// <summary>
/// Session of a client who verified a code. The token opens one booking only.
/// </summary>
/// <param name="AccessToken">Short-lived token sent as <c>Authorization: Bearer</c>.</param>
/// <param name="ExpiresAt">Instant the token stops being accepted (UTC).</param>
public sealed record PortalSessionResponse(string AccessToken, DateTimeOffset ExpiresAt);
