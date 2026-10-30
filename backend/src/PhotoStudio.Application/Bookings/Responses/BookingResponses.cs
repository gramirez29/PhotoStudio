using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Bookings.Responses;

/// <summary>
/// Extra sold with a booking, as exposed by the API.
/// </summary>
/// <param name="Code">Stable code of the extra.</param>
/// <param name="Name">Name of the extra.</param>
/// <param name="UnitPrice">Price of one unit when the booking was made.</param>
/// <param name="Quantity">Number of units.</param>
/// <param name="Total">Price of every unit together.</param>
public sealed record BookingExtraResponse(string Code, string Name, MoneyResponse UnitPrice, int Quantity, MoneyResponse Total);

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
/// <param name="Reference">Reference of the transfer given by the client with a SINPE Móvil proof, if any.</param>
public sealed record PaymentResponse(
    Guid Id,
    MoneyResponse Amount,
    PaymentMethod Method,
    Channel Channel,
    PaymentStatus Status,
    DateTimeOffset RecordedAt,
    string? Reference = null);

/// <summary>
/// Contract signature as exposed by the API.
/// </summary>
/// <param name="SignerName">Name typed by the signer.</param>
/// <param name="TemplateVersion">Signed template version.</param>
/// <param name="Channel">Channel of the signature.</param>
/// <param name="SignedAt">Instant of the signature (UTC).</param>
/// <param name="Declaration">What the client declared, when they signed through the portal.</param>
public sealed record ContractResponse(
    string SignerName,
    string TemplateVersion,
    Channel Channel,
    DateTimeOffset SignedAt,
    ClientDeclarationResponse? Declaration = null);

/// <summary>
/// What the client declared when signing the contract through the portal, as the photographer sees it.
/// </summary>
/// <param name="IdNumber">Identity number of the signer, digits only.</param>
/// <param name="ParticipantCount">Number of people in the session.</param>
/// <param name="SessionLocation">Place of the session, if the client wrote one.</param>
/// <param name="ImageUse">What the client allowed to do with the photos.</param>
public sealed record ClientDeclarationResponse(string IdNumber, int ParticipantCount, string? SessionLocation, ImageUse ImageUse);

/// <summary>
/// Contract of a booking as the photographer reads it.
/// </summary>
/// <param name="TemplateVersion">Version of the template; the app sends it back when the client signs, so they sign what they read.</param>
/// <param name="Text">Text of the contract with the booking data filled in. Before signing, what the client still has to declare is shown as blanks; after signing it is the exact text that was signed, or empty for a contract registered on paper.</param>
/// <param name="Signed">Whether the contract is already signed.</param>
public sealed record BookingContractResponse(string TemplateVersion, string Text, bool Signed);

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
/// <param name="ClientEmail">Client email, or <see langword="null"/> when the booking has none.</param>
/// <param name="PackageTier">Package the client booked, or <see langword="null"/> for bookings made before it was recorded.</param>
/// <param name="BasePrice">Price of the package alone; <c>PackagePrice</c> is what the client pays: this plus the extras minus the discount.</param>
/// <param name="Extras">Extras sold with the booking.</param>
/// <param name="Discount">Discount given to the client; zero when there is none.</param>
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
    IReadOnlyList<BookingAction> AllowedActions,
    string? ClientEmail = null,
    PackageTier? PackageTier = null,
    MoneyResponse? BasePrice = null,
    IReadOnlyList<BookingExtraResponse>? Extras = null,
    MoneyResponse? Discount = null);

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

/// <summary>
/// One page of the bookings of a photographer, the most recently created first.
/// </summary>
/// <param name="Items">The bookings of this page.</param>
/// <param name="NextCursor">What to send back to get the next page, or <see langword="null"/> when this is the last one.</param>
public sealed record BookingListResponse(IReadOnlyList<BookingSummaryResponse> Items, string? NextCursor);
