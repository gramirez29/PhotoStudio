using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Billing;

namespace PhotoStudio.Application.Billing.CompleteRefund;

/// <summary>
/// Records that a refund was given back. It is saved with optimistic concurrency, so completing it twice at the same time
/// (a double tap) records it once: the second request finds it already completed and is rejected by the domain.
/// </summary>
/// <param name="settlements">Settlement repository.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class CompleteRefundHandler(ISettlementRepository settlements, TimeProvider timeProvider)
    : ICommandHandler<CompleteRefundCommand, SettlementResponse>
{
    /// <summary>
    /// Completes the refund.
    /// </summary>
    /// <param name="command">Booking, method and note.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The settlement after the refund was recorded.</returns>
    /// <exception cref="NotFoundException">When the booking has no settlement or it is not the photographer's.</exception>
    /// <exception cref="ConflictException">When the settlement changed while it was being saved.</exception>
    public async Task<SettlementResponse> HandleAsync(CompleteRefundCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var settlement = await settlements.GetByBookingIdAsync(command.BookingId, cancellationToken);
        if (settlement is null || settlement.PhotographerId != command.PhotographerId)
        {
            throw new NotFoundException(nameof(BookingSettlement), command.BookingId);
        }

        settlement.CompleteRefund(command.Method, command.Note, timeProvider.GetUtcNow());
        await settlements.UpdateAsync(settlement, cancellationToken);

        return settlement.ToResponse();
    }
}
