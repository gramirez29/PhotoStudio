using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Infrastructure.Persistence.Documents;

/// <summary>
/// Maps between the <see cref="Booking"/> aggregate and its MongoDB data model.
/// </summary>
public static class BookingDocumentMappings
{
    /// <summary>
    /// Maps a booking to its document.
    /// </summary>
    /// <param name="booking">Booking to map.</param>
    /// <param name="version">Version to store in the document.</param>
    /// <returns>The document.</returns>
    public static BookingDocument ToDocument(this Booking booking, long version)
    {
        ArgumentNullException.ThrowIfNull(booking);

        return new BookingDocument
        {
            Id = booking.Id,
            Version = version,
            PhotographerId = booking.PhotographerId,
            ClientName = booking.Client.Name,
            ClientPhone = booking.Client.Phone,
            PackageName = booking.PackageName,
            PackagePrice = booking.PackagePrice.Amount,
            Currency = booking.PackagePrice.Currency,
            SlotStart = booking.Slot.Start.UtcDateTime,
            SlotEnd = booking.Slot.End.UtcDateTime,
            Policy = booking.Policy.ToDocument(),
            Status = booking.Status.ToString(),
            CreatedAt = booking.CreatedAt.UtcDateTime,
            ExpiresAt = booking.ExpiresAt?.UtcDateTime,
            Contract = booking.Contract?.ToDocument(),
            RescheduleCount = booking.RescheduleCount,
            ClientAbsentMarkedAt = booking.ClientAbsentMarkedAt?.UtcDateTime,
            Payments = [.. booking.Payments.Select(payment => payment.ToDocument())],
            History = [.. booking.History.Select(transition => transition.ToDocument())],
        };
    }

    /// <summary>
    /// Rebuilds the booking aggregate from its document.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The booking.</returns>
    public static Booking ToDomain(this BookingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return Booking.Restore(
            document.Id,
            document.Version,
            document.PhotographerId,
            ClientContact.Create(document.ClientName, document.ClientPhone),
            document.PackageName,
            Money.Restore(document.PackagePrice, document.Currency),
            TimeSlot.Create(ToOffset(document.SlotStart), ToOffset(document.SlotEnd)),
            document.Policy.ToDomain(),
            Enum.Parse<BookingStatus>(document.Status),
            ToOffset(document.CreatedAt),
            ToNullableOffset(document.ExpiresAt),
            document.Contract?.ToDomain(),
            document.RescheduleCount,
            ToNullableOffset(document.ClientAbsentMarkedAt),
            document.Payments.Select(payment => payment.ToDomain()),
            document.History.Select(transition => transition.ToDomain()));
    }

    /// <summary>
    /// Maps a policy to its document.
    /// </summary>
    /// <param name="policy">Policy to map.</param>
    /// <returns>The document.</returns>
    private static BookingPolicyDocument ToDocument(this BookingPolicy policy) => new()
    {
        TentativeHoldHours = policy.TentativeHoldHours,
        DepositPercentage = policy.DepositPercentage,
        FreeCancellationWindowHours = policy.FreeCancellationWindowHours,
        RescheduleMinNoticeHours = policy.RescheduleMinNoticeHours,
        MaxReschedules = policy.MaxReschedules,
        ClientAbsentToleranceMinutes = policy.ClientAbsentToleranceMinutes,
        ClientAbsentRevertWindowDays = policy.ClientAbsentRevertWindowDays,
    };

    /// <summary>
    /// Maps a policy document to the domain policy.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The policy.</returns>
    private static BookingPolicy ToDomain(this BookingPolicyDocument document) => BookingPolicy.Create(
        document.TentativeHoldHours,
        document.DepositPercentage,
        document.FreeCancellationWindowHours,
        document.RescheduleMinNoticeHours,
        document.MaxReschedules,
        document.ClientAbsentToleranceMinutes,
        document.ClientAbsentRevertWindowDays);

    /// <summary>
    /// Maps a contract signature to its document.
    /// </summary>
    /// <param name="contract">Signature to map.</param>
    /// <returns>The document.</returns>
    private static ContractSignatureDocument ToDocument(this ContractSignature contract) => new()
    {
        SignerName = contract.SignerName,
        TemplateVersion = contract.TemplateVersion,
        SignedBy = contract.SignedBy.ToString(),
        Channel = contract.Channel.ToString(),
        SignedAt = contract.SignedAt.UtcDateTime,
    };

    /// <summary>
    /// Maps a contract document to the domain signature.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The signature.</returns>
    private static ContractSignature ToDomain(this ContractSignatureDocument document) => ContractSignature.Create(
        document.SignerName,
        document.TemplateVersion,
        Enum.Parse<Actor>(document.SignedBy),
        Enum.Parse<Channel>(document.Channel),
        ToOffset(document.SignedAt));

    /// <summary>
    /// Maps a payment to its document.
    /// </summary>
    /// <param name="payment">Payment to map.</param>
    /// <returns>The document.</returns>
    private static PaymentDocument ToDocument(this Payment payment) => new()
    {
        Id = payment.Id,
        Amount = payment.Amount.Amount,
        Currency = payment.Amount.Currency,
        Method = payment.Method.ToString(),
        Channel = payment.Channel.ToString(),
        RecordedBy = payment.RecordedBy.ToString(),
        Status = payment.Status.ToString(),
        IdempotencyKey = payment.IdempotencyKey,
        RecordedAt = payment.RecordedAt.UtcDateTime,
        ResolvedAt = payment.ResolvedAt?.UtcDateTime,
        RejectionReason = payment.RejectionReason,
    };

    /// <summary>
    /// Maps a payment document to the domain payment.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The payment.</returns>
    private static Payment ToDomain(this PaymentDocument document) => Payment.Restore(
        document.Id,
        Money.Restore(document.Amount, document.Currency),
        Enum.Parse<PaymentMethod>(document.Method),
        Enum.Parse<Channel>(document.Channel),
        Enum.Parse<Actor>(document.RecordedBy),
        Enum.Parse<PaymentStatus>(document.Status),
        document.IdempotencyKey,
        ToOffset(document.RecordedAt),
        ToNullableOffset(document.ResolvedAt),
        document.RejectionReason);

    /// <summary>
    /// Maps a transition to its document.
    /// </summary>
    /// <param name="transition">Transition to map.</param>
    /// <returns>The document.</returns>
    private static StatusTransitionDocument ToDocument(this StatusTransition transition) => new()
    {
        From = transition.From?.ToString(),
        To = transition.To.ToString(),
        Actor = transition.Actor.ToString(),
        Channel = transition.Channel?.ToString(),
        Reason = transition.Reason,
        OccurredAt = transition.OccurredAt.UtcDateTime,
    };

    /// <summary>
    /// Maps a transition document to the domain transition.
    /// </summary>
    /// <param name="document">Document to map.</param>
    /// <returns>The transition.</returns>
    private static StatusTransition ToDomain(this StatusTransitionDocument document) => new(
        document.From is null ? null : Enum.Parse<BookingStatus>(document.From),
        Enum.Parse<BookingStatus>(document.To),
        Enum.Parse<Actor>(document.Actor),
        document.Channel is null ? null : Enum.Parse<Channel>(document.Channel),
        document.Reason,
        ToOffset(document.OccurredAt));

    /// <summary>
    /// Converts a stored date to a UTC instant.
    /// </summary>
    /// <param name="value">Stored date.</param>
    /// <returns>The UTC instant.</returns>
    private static DateTimeOffset ToOffset(DateTime value) =>
        new(DateTime.SpecifyKind(value, DateTimeKind.Utc));

    /// <summary>
    /// Converts an optional stored date to an optional UTC instant.
    /// </summary>
    /// <param name="value">Stored date, if any.</param>
    /// <returns>The UTC instant, or <see langword="null"/>.</returns>
    private static DateTimeOffset? ToNullableOffset(DateTime? value) =>
        value is null ? null : ToOffset(value.Value);
}
