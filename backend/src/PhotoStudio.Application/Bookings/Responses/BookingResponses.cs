using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.Responses;

/// <summary>
/// Monetary amount as exposed by the API.
/// </summary>
/// <param name="Amount">Numeric amount.</param>
/// <param name="Currency">ISO 4217 currency code.</param>
public sealed record MoneyResponse(decimal Amount, string Currency);

/// <summary>
/// Payment as exposed by the API.
/// </summary>
/// <param name="Id">Payment identifier.</param>
/// <param name="Amount">Paid amount.</param>
/// <param name="Method">Payment method.</param>
/// <param name="Channel">Channel of the payment.</param>
/// <param name="Status">Verification status.</param>
/// <param name="RecordedAt">Instant the payment was recorded (UTC).</param>
public sealed record PaymentResponse(
    Guid Id,
    MoneyResponse Amount,
    PaymentMethod Method,
    Channel Channel,
    PaymentStatus Status,
    DateTimeOffset RecordedAt);

/// <summary>
/// Contract signature as exposed by the API.
/// </summary>
/// <param name="SignerName">Name typed by the signer.</param>
/// <param name="TemplateVersion">Signed template version.</param>
/// <param name="Channel">Channel of the signature.</param>
/// <param name="SignedAt">Instant of the signature (UTC).</param>
public sealed record ContractResponse(string SignerName, string TemplateVersion, Channel Channel, DateTimeOffset SignedAt);

/// <summary>
/// Booking as exposed by the API. The mobile app mirrors this shape in TypeScript.
/// </summary>
/// <param name="Id">Booking identifier.</param>
/// <param name="PhotographerId">Photographer identifier.</param>
/// <param name="ClientName">Client name.</param>
/// <param name="ClientPhone">Client phone.</param>
/// <param name="PackageName">Package name.</param>
/// <param name="Status">Current status.</param>
/// <param name="SessionStart">Session start (UTC).</param>
/// <param name="SessionEnd">Session end (UTC).</param>
/// <param name="ExpiresAt">End of the tentative hold, if any (UTC).</param>
/// <param name="PackagePrice">Package price.</param>
/// <param name="DepositRequired">Deposit required to confirm.</param>
/// <param name="TotalPaid">Sum of verified payments.</param>
/// <param name="Balance">Outstanding balance.</param>
/// <param name="Contract">Contract signature, if signed.</param>
/// <param name="Payments">Recorded payments.</param>
/// <param name="AllowedActions">Actions the caller can perform right now.</param>
public sealed record BookingResponse(
    Guid Id,
    Guid PhotographerId,
    string ClientName,
    string ClientPhone,
    string PackageName,
    BookingStatus Status,
    DateTimeOffset SessionStart,
    DateTimeOffset SessionEnd,
    DateTimeOffset? ExpiresAt,
    MoneyResponse PackagePrice,
    MoneyResponse DepositRequired,
    MoneyResponse TotalPaid,
    MoneyResponse Balance,
    ContractResponse? Contract,
    IReadOnlyList<PaymentResponse> Payments,
    IReadOnlyList<BookingAction> AllowedActions);

/// <summary>
/// Compact booking for lists. The mobile app mirrors this shape in TypeScript; the full detail comes from <see cref="BookingResponse"/>.
/// </summary>
/// <param name="Id">Booking identifier.</param>
/// <param name="ClientName">Client name.</param>
/// <param name="PackageName">Package name.</param>
/// <param name="Status">Current status.</param>
/// <param name="SessionStart">Session start (UTC).</param>
/// <param name="SessionEnd">Session end (UTC).</param>
/// <param name="PackagePrice">Package price.</param>
/// <param name="Balance">Outstanding balance.</param>
public sealed record BookingSummaryResponse(
    Guid Id,
    string ClientName,
    string PackageName,
    BookingStatus Status,
    DateTimeOffset SessionStart,
    DateTimeOffset SessionEnd,
    MoneyResponse PackagePrice,
    MoneyResponse Balance);
