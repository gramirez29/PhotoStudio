using PhotoStudio.Domain.Bookings;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Application.Bookings.Responses;

/// <summary>
/// Maps domain objects to API response DTOs.
/// </summary>
public static class BookingResponseMappings
{
    /// <summary>
    /// Maps a booking to its response, including the actions allowed for the caller.
    /// </summary>
    /// <param name="booking">Booking to map.</param>
    /// <param name="actor">Actor that will receive the response.</param>
    /// <param name="now">Current instant, used to compute the allowed actions.</param>
    /// <returns>The response DTO.</returns>
    public static BookingResponse ToResponse(this Booking booking, Actor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(booking);

        return new BookingResponse(
            booking.Id,
            booking.PhotographerId,
            booking.Client.Name,
            booking.Client.Phone,
            booking.PackageName,
            booking.Status,
            booking.Slot.Start,
            booking.Slot.End,
            booking.ExpiresAt,
            booking.PackagePrice.ToResponse(),
            booking.DepositRequired.ToResponse(),
            booking.TotalVerifiedPaid.ToResponse(),
            booking.Balance.ToResponse(),
            booking.Contract?.ToResponse(),
            [.. booking.Payments.Select(payment => payment.ToResponse())],
            booking.GetAllowedActions(actor, now));
    }

    /// <summary>
    /// Maps an amount to its response.
    /// </summary>
    /// <param name="money">Amount to map.</param>
    /// <returns>The response DTO.</returns>
    public static MoneyResponse ToResponse(this Money money)
    {
        ArgumentNullException.ThrowIfNull(money);
        return new MoneyResponse(money.Amount, money.Currency);
    }

    /// <summary>
    /// Maps a payment to its response.
    /// </summary>
    /// <param name="payment">Payment to map.</param>
    /// <returns>The response DTO.</returns>
    public static PaymentResponse ToResponse(this Payment payment)
    {
        ArgumentNullException.ThrowIfNull(payment);
        return new PaymentResponse(payment.Id, payment.Amount.ToResponse(), payment.Method, payment.Channel, payment.Status, payment.RecordedAt);
    }

    /// <summary>
    /// Maps a contract signature to its response.
    /// </summary>
    /// <param name="contract">Signature to map.</param>
    /// <returns>The response DTO.</returns>
    public static ContractResponse ToResponse(this ContractSignature contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        return new ContractResponse(contract.SignerName, contract.TemplateVersion, contract.Channel, contract.SignedAt);
    }
}
