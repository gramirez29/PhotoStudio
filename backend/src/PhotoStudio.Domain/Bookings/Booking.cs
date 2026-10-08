using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Bookings;

/// <summary>
/// Booking aggregate. Implements the booking state machine (transitions B1 to B13 of the design workbook):
/// every change of status goes through a method that validates the current status, the actor and the guards,
/// records an audit entry and raises a domain event.
/// </summary>
public sealed class Booking : AggregateRoot<Guid>
{
    private readonly List<Payment> _payments;
    private readonly List<StatusTransition> _history;

    /// <summary>
    /// Initializes a new instance of the <see cref="Booking"/> class. Use <see cref="Create"/> or <see cref="Restore"/>.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="version">Persisted version.</param>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="client">Client contact.</param>
    /// <param name="packageName">Name of the booked package.</param>
    /// <param name="packagePrice">Price of the package.</param>
    /// <param name="slot">Session slot.</param>
    /// <param name="policy">Policy copied into the booking.</param>
    /// <param name="status">Current status.</param>
    /// <param name="createdAt">Creation instant.</param>
    /// <param name="expiresAt">End of the tentative hold, if any.</param>
    /// <param name="contract">Contract signature, if signed.</param>
    /// <param name="rescheduleCount">Number of reschedules performed.</param>
    /// <param name="clientAbsentMarkedAt">Instant the client was marked absent, if any.</param>
    /// <param name="payments">Payments recorded against the booking.</param>
    /// <param name="history">Transition audit trail.</param>
    private Booking(
        Guid id,
        long version,
        Guid photographerId,
        ClientContact client,
        string packageName,
        Money packagePrice,
        TimeSlot slot,
        BookingPolicy policy,
        BookingStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt,
        ContractSignature? contract,
        int rescheduleCount,
        DateTimeOffset? clientAbsentMarkedAt,
        List<Payment> payments,
        List<StatusTransition> history)
        : base(id, version)
    {
        PhotographerId = photographerId;
        Client = client;
        PackageName = packageName;
        PackagePrice = packagePrice;
        Slot = slot;
        Policy = policy;
        Status = status;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        Contract = contract;
        RescheduleCount = rescheduleCount;
        ClientAbsentMarkedAt = clientAbsentMarkedAt;
        _payments = payments;
        _history = history;
    }

    /// <summary>Gets the photographer (tenant) that owns the booking.</summary>
    public Guid PhotographerId { get; }

    /// <summary>Gets the client contact.</summary>
    public ClientContact Client { get; }

    /// <summary>Gets the name of the booked package.</summary>
    public string PackageName { get; }

    /// <summary>Gets the price of the package, fixed when the booking was created.</summary>
    public Money PackagePrice { get; }

    /// <summary>Gets the session slot.</summary>
    public TimeSlot Slot { get; private set; }

    /// <summary>Gets the policy copied into the booking when it was created.</summary>
    public BookingPolicy Policy { get; }

    /// <summary>Gets the current status.</summary>
    public BookingStatus Status { get; private set; }

    /// <summary>Gets the creation instant (UTC).</summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>Gets the instant the tentative hold ends; <see langword="null"/> once the booking leaves <see cref="BookingStatus.Tentative"/>.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>Gets the contract signature, or <see langword="null"/> when the contract is not signed yet.</summary>
    public ContractSignature? Contract { get; private set; }

    /// <summary>Gets how many times the session was rescheduled.</summary>
    public int RescheduleCount { get; private set; }

    /// <summary>Gets the instant the client was marked absent, while the booking is in <see cref="BookingStatus.ClientAbsent"/>.</summary>
    public DateTimeOffset? ClientAbsentMarkedAt { get; private set; }

    /// <summary>Gets the payments recorded against the booking.</summary>
    public IReadOnlyList<Payment> Payments => _payments.AsReadOnly();

    /// <summary>Gets the transition audit trail.</summary>
    public IReadOnlyList<StatusTransition> History => _history.AsReadOnly();

    /// <summary>Gets the deposit required to confirm the booking.</summary>
    public Money DepositRequired => PackagePrice.MultiplyBy(Policy.DepositPercentage);

    /// <summary>Gets the sum of the verified payments.</summary>
    public Money TotalVerifiedPaid => _payments
        .Where(payment => payment.Status == PaymentStatus.Verified)
        .Aggregate(Money.Zero(PackagePrice.Currency), (total, payment) => total.Add(payment.Amount));

    /// <summary>Gets the outstanding balance (package price minus verified payments). Negative means overpaid.</summary>
    public Money Balance => PackagePrice.Subtract(TotalVerifiedPaid);

    /// <summary>Gets a value indicating whether any proof of payment is waiting for verification.</summary>
    public bool HasPendingPaymentVerification => _payments.Exists(payment => payment.Status == PaymentStatus.PendingVerification);

    /// <summary>Gets a value indicating whether the verified payments cover the required deposit.</summary>
    public bool IsDepositCovered => TotalVerifiedPaid.IsGreaterThanOrEqualTo(DepositRequired);

    /// <summary>
    /// B1. Creates a tentative booking that holds the slot until <see cref="ExpiresAt"/>.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="photographerId">Photographer (tenant) identifier.</param>
    /// <param name="client">Client contact.</param>
    /// <param name="packageName">Name of the booked package.</param>
    /// <param name="packagePrice">Price of the package.</param>
    /// <param name="slot">Session slot; must start in the future.</param>
    /// <param name="policy">Policy to copy into the booking.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The new booking in <see cref="BookingStatus.Tentative"/>.</returns>
    /// <exception cref="DomainException">When a value is missing or the session is not in the future.</exception>
    public static Booking Create(
        Guid id,
        Guid photographerId,
        ClientContact client,
        string packageName,
        Money packagePrice,
        TimeSlot slot,
        BookingPolicy policy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(packagePrice);
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(policy);

        if (id == Guid.Empty || photographerId == Guid.Empty)
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "Booking and photographer identifiers are required.");
        }

        if (string.IsNullOrWhiteSpace(packageName))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The package name is required.");
        }

        if (slot.Start <= now)
        {
            throw new DomainException(DomainErrorCodes.SessionInPast, "The session must start in the future.");
        }

        var holdEnd = now.AddHours(policy.TentativeHoldHours);
        var expiresAt = holdEnd < slot.Start ? holdEnd : slot.Start;

        var booking = new Booking(
            id,
            0,
            photographerId,
            client,
            packageName.Trim(),
            packagePrice,
            slot,
            policy,
            BookingStatus.Tentative,
            now,
            expiresAt,
            null,
            0,
            null,
            [],
            []);

        booking._history.Add(new StatusTransition(null, BookingStatus.Tentative, Actor.Photographer, Channel.PhotographerApp, null, now));
        booking.Raise(new BookingCreated(id, photographerId, slot.Start, expiresAt, now));
        return booking;
    }

    /// <summary>
    /// Rebuilds a booking from persisted data. Performs no validation and raises no events.
    /// </summary>
    /// <param name="id">Booking identifier.</param>
    /// <param name="version">Persisted version.</param>
    /// <param name="photographerId">Photographer identifier.</param>
    /// <param name="client">Client contact.</param>
    /// <param name="packageName">Package name.</param>
    /// <param name="packagePrice">Package price.</param>
    /// <param name="slot">Session slot.</param>
    /// <param name="policy">Copied policy.</param>
    /// <param name="status">Current status.</param>
    /// <param name="createdAt">Creation instant.</param>
    /// <param name="expiresAt">End of the tentative hold.</param>
    /// <param name="contract">Contract signature.</param>
    /// <param name="rescheduleCount">Reschedule count.</param>
    /// <param name="clientAbsentMarkedAt">Instant the client was marked absent.</param>
    /// <param name="payments">Recorded payments.</param>
    /// <param name="history">Transition audit trail.</param>
    /// <returns>The restored booking.</returns>
    public static Booking Restore(
        Guid id,
        long version,
        Guid photographerId,
        ClientContact client,
        string packageName,
        Money packagePrice,
        TimeSlot slot,
        BookingPolicy policy,
        BookingStatus status,
        DateTimeOffset createdAt,
        DateTimeOffset? expiresAt,
        ContractSignature? contract,
        int rescheduleCount,
        DateTimeOffset? clientAbsentMarkedAt,
        IEnumerable<Payment> payments,
        IEnumerable<StatusTransition> history) =>
        new(
            id,
            version,
            photographerId,
            client,
            packageName,
            packagePrice,
            slot,
            policy,
            status,
            createdAt,
            expiresAt,
            contract,
            rescheduleCount,
            clientAbsentMarkedAt,
            [.. payments],
            [.. history]);

    /// <summary>
    /// B2. Registers the contract signature and confirms the booking when the deposit is already covered.
    /// </summary>
    /// <param name="signerName">Name typed by the signer.</param>
    /// <param name="templateVersion">Contract template version.</param>
    /// <param name="actor">Client (through the portal) or photographer (in person or external paper contract).</param>
    /// <param name="channel">Channel of the signature; must match the actor.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the status, actor, channel or contract state does not allow signing.</exception>
    public void SignContract(string signerName, string templateVersion, Actor actor, Channel channel, DateTimeOffset now)
    {
        EnsureStatus(nameof(SignContract), BookingStatus.Tentative);

        var channelMatchesActor = actor switch
        {
            Actor.Client => channel == Channel.Portal,
            Actor.Photographer => channel is Channel.InPerson or Channel.External,
            _ => false,
        };

        if (!channelMatchesActor)
        {
            throw new DomainException(DomainErrorCodes.ActorNotAllowed, $"{actor} cannot sign the contract through {channel}.");
        }

        if (Contract is not null)
        {
            throw new DomainException(DomainErrorCodes.ContractAlreadySigned, "The contract is already signed.");
        }

        Contract = ContractSignature.Create(signerName, templateVersion, actor, channel, now);
        Raise(new ContractSigned(Id, Contract.TemplateVersion, channel, now));
        TryConfirm(now);
    }

    /// <summary>
    /// B3. Registers a SINPE Móvil proof uploaded by the client. Retrying with the same idempotency key returns the original payment.
    /// </summary>
    /// <param name="paymentId">Identifier for the new payment.</param>
    /// <param name="amount">Declared amount.</param>
    /// <param name="method">Payment method; only <see cref="PaymentMethod.SinpeMovil"/> proofs are accepted.</param>
    /// <param name="idempotencyKey">Idempotency key of the request.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The pending payment, or the existing one for a repeated idempotency key.</returns>
    /// <exception cref="DomainException">When the status, method or amount is not valid.</exception>
    public Payment SubmitPaymentProof(Guid paymentId, Money amount, PaymentMethod method, string idempotencyKey, DateTimeOffset now)
    {
        EnsureStatus(nameof(SubmitPaymentProof), BookingStatus.Tentative, BookingStatus.Confirmed, BookingStatus.Completed);

        var existing = FindByIdempotencyKey(idempotencyKey);
        if (existing is not null)
        {
            return existing;
        }

        if (method != PaymentMethod.SinpeMovil)
        {
            throw new DomainException(DomainErrorCodes.InvalidPaymentMethod, "Only SINPE Móvil proofs can be submitted for verification.");
        }

        EnsurePositiveAmountInBookingCurrency(amount);

        var payment = Payment.CreatePending(paymentId, amount, method, idempotencyKey.Trim(), now);
        _payments.Add(payment);
        Raise(new PaymentSubmitted(Id, payment.Id, amount.Amount, amount.Currency, now));
        return payment;
    }

    /// <summary>
    /// B4. Verifies a pending proof of payment and confirms the booking when every condition is met.
    /// </summary>
    /// <param name="paymentId">Identifier of the pending payment.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the status does not allow it or the payment is not pending.</exception>
    public void VerifyPayment(Guid paymentId, DateTimeOffset now)
    {
        EnsureStatus(nameof(VerifyPayment), BookingStatus.Tentative, BookingStatus.Confirmed, BookingStatus.Completed);

        var payment = GetPayment(paymentId);
        payment.Verify(now);
        Raise(new PaymentVerified(Id, payment.Id, payment.Amount.Amount, payment.Amount.Currency, payment.Method, payment.Channel, now));
        TryConfirm(now);
    }

    /// <summary>
    /// B4. Rejects a pending proof of payment. The payment stays in the history as evidence.
    /// </summary>
    /// <param name="paymentId">Identifier of the pending payment.</param>
    /// <param name="reason">Why the proof is rejected.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the status does not allow it, the reason is empty or the payment is not pending.</exception>
    public void RejectPayment(Guid paymentId, string reason, DateTimeOffset now)
    {
        EnsureStatus(nameof(RejectPayment), BookingStatus.Tentative, BookingStatus.Confirmed, BookingStatus.Completed);
        var trimmedReason = RequireReason(reason);

        var payment = GetPayment(paymentId);
        payment.Reject(trimmedReason, now);
        Raise(new PaymentRejected(Id, payment.Id, trimmedReason, now));
    }

    /// <summary>
    /// B5. Records a payment received face to face. It is verified immediately and can confirm the booking.
    /// Retrying with the same idempotency key returns the original payment.
    /// </summary>
    /// <param name="paymentId">Identifier for the new payment.</param>
    /// <param name="amount">Received amount.</param>
    /// <param name="method"><see cref="PaymentMethod.Cash"/> or <see cref="PaymentMethod.SinpeMovil"/>.</param>
    /// <param name="idempotencyKey">Idempotency key of the request.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The verified payment, or the existing one for a repeated idempotency key.</returns>
    /// <exception cref="DomainException">When the status, method or amount is not valid.</exception>
    public Payment RecordInPersonPayment(Guid paymentId, Money amount, PaymentMethod method, string idempotencyKey, DateTimeOffset now)
    {
        EnsureStatus(nameof(RecordInPersonPayment), BookingStatus.Tentative, BookingStatus.Confirmed, BookingStatus.Completed);

        var existing = FindByIdempotencyKey(idempotencyKey);
        if (existing is not null)
        {
            return existing;
        }

        if (method is not (PaymentMethod.Cash or PaymentMethod.SinpeMovil))
        {
            throw new DomainException(DomainErrorCodes.InvalidPaymentMethod, "In-person payments must be cash or SINPE Móvil.");
        }

        EnsurePositiveAmountInBookingCurrency(amount);

        var payment = Payment.CreateVerifiedInPerson(paymentId, amount, method, idempotencyKey.Trim(), now);
        _payments.Add(payment);
        Raise(new PaymentVerified(Id, payment.Id, amount.Amount, amount.Currency, method, payment.Channel, now));
        TryConfirm(now);
        return payment;
    }

    /// <summary>
    /// B7. Expires a tentative booking whose hold ended. Executed by a background job, so every guard is re-evaluated at run time.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the booking is not tentative, the hold has not ended or a proof is pending verification.</exception>
    public void Expire(DateTimeOffset now)
    {
        EnsureStatus(nameof(Expire), BookingStatus.Tentative);

        if (ExpiresAt is null || now < ExpiresAt.Value)
        {
            throw new DomainException(DomainErrorCodes.GuardFailed, "The tentative hold has not ended yet.");
        }

        if (HasPendingPaymentVerification)
        {
            throw new DomainException(DomainErrorCodes.GuardFailed, "A proof of payment is waiting for verification; the booking cannot expire.");
        }

        TransitionTo(BookingStatus.Expired, Actor.System, null, null, now);
        Raise(new BookingExpired(Id, now));
    }

    /// <summary>
    /// B8 and B10. Cancels a tentative or confirmed booking. A reason is mandatory once the booking is confirmed.
    /// The economic outcome (refund or retention) is decided by the billing module from the event.
    /// </summary>
    /// <param name="actor">Client or photographer.</param>
    /// <param name="reason">Cancellation reason; required for confirmed bookings.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the status or actor does not allow it, or the reason is missing.</exception>
    public void Cancel(Actor actor, string? reason, DateTimeOffset now)
    {
        EnsureStatus(nameof(Cancel), BookingStatus.Tentative, BookingStatus.Confirmed);
        EnsureActor(nameof(Cancel), actor, Actor.Client, Actor.Photographer);

        var wasConfirmed = Status == BookingStatus.Confirmed;
        var trimmedReason = wasConfirmed ? RequireReason(reason) : reason?.Trim();
        var hoursBeforeSession = (Slot.Start - now).TotalHours;

        ExpiresAt = null;
        TransitionTo(BookingStatus.Cancelled, actor, ChannelFor(actor), trimmedReason, now);
        Raise(new BookingCancelled(Id, actor, wasConfirmed, hoursBeforeSession, trimmedReason, now));
    }

    /// <summary>
    /// B9. Moves a confirmed booking to another slot. Clients are limited by the notice and count of the policy;
    /// the photographer is not. Slot availability is checked by the application layer before calling this method.
    /// </summary>
    /// <param name="newSlot">New session slot; must start in the future.</param>
    /// <param name="actor">Client or photographer.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the status, actor or policy does not allow it.</exception>
    public void Reschedule(TimeSlot newSlot, Actor actor, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(newSlot);
        EnsureStatus(nameof(Reschedule), BookingStatus.Confirmed);
        EnsureActor(nameof(Reschedule), actor, Actor.Client, Actor.Photographer);

        if (newSlot.Start <= now)
        {
            throw new DomainException(DomainErrorCodes.SessionInPast, "The new session must start in the future.");
        }

        if (actor == Actor.Client && !ClientCanReschedule(now))
        {
            throw new DomainException(DomainErrorCodes.GuardFailed, "The reschedule limit or the minimum notice of the policy was reached.");
        }

        var previousStart = Slot.Start;
        Slot = newSlot;
        RescheduleCount++;
        _history.Add(new StatusTransition(BookingStatus.Confirmed, BookingStatus.Confirmed, actor, ChannelFor(actor), "Rescheduled", now));
        Raise(new BookingRescheduled(Id, previousStart, newSlot.Start, actor, now));
    }

    /// <summary>
    /// B11. Marks the session as completed once it has started.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the booking is not confirmed or the session has not started.</exception>
    public void Complete(DateTimeOffset now)
    {
        EnsureStatus(nameof(Complete), BookingStatus.Confirmed);

        if (now < Slot.Start)
        {
            throw new DomainException(DomainErrorCodes.GuardFailed, "The session has not started yet.");
        }

        TransitionTo(BookingStatus.Completed, Actor.Photographer, Channel.PhotographerApp, null, now);
        Raise(new SessionCompleted(Id, PhotographerId, now));
    }

    /// <summary>
    /// B12. Marks that the client did not show up, once the tolerance of the policy has passed.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the booking is not confirmed or the tolerance has not passed.</exception>
    public void MarkClientAbsent(DateTimeOffset now)
    {
        EnsureStatus(nameof(MarkClientAbsent), BookingStatus.Confirmed);

        if (now < ClientAbsentAllowedFrom)
        {
            throw new DomainException(DomainErrorCodes.GuardFailed, "The client-absent tolerance has not passed yet.");
        }

        ClientAbsentMarkedAt = now;
        TransitionTo(BookingStatus.ClientAbsent, Actor.Photographer, Channel.PhotographerApp, null, now);
        Raise(new ClientMarkedAbsent(Id, now));
    }

    /// <summary>
    /// B13. Reverts a client-absent mark made by mistake, within the window of the policy. The booking returns to <see cref="BookingStatus.Confirmed"/>.
    /// </summary>
    /// <param name="reason">Why the client-absent mark is reverted.</param>
    /// <param name="now">Current instant.</param>
    /// <exception cref="DomainException">When the client is not marked absent, the reason is empty or the window has closed.</exception>
    public void RevertClientAbsent(string reason, DateTimeOffset now)
    {
        EnsureStatus(nameof(RevertClientAbsent), BookingStatus.ClientAbsent);
        var trimmedReason = RequireReason(reason);

        if (!CanRevertClientAbsent(now))
        {
            throw new DomainException(DomainErrorCodes.GuardFailed, "The window to revert the client-absent mark has closed.");
        }

        ClientAbsentMarkedAt = null;
        TransitionTo(BookingStatus.Confirmed, Actor.Photographer, Channel.PhotographerApp, trimmedReason, now);
        Raise(new ClientAbsenceReverted(Id, trimmedReason, now));
    }

    /// <summary>
    /// Computes the actions the given actor can perform right now, so clients only render allowed buttons.
    /// </summary>
    /// <param name="actor">Actor asking for the actions.</param>
    /// <param name="now">Current instant.</param>
    /// <returns>The allowed actions, in a stable order.</returns>
    public IReadOnlyList<BookingAction> GetAllowedActions(Actor actor, DateTimeOffset now)
    {
        List<BookingAction> actions = [];
        var isParticipant = actor is Actor.Client or Actor.Photographer;
        var hasBalance = Balance.Amount > 0m;

        switch (Status)
        {
            case BookingStatus.Tentative:
                if (isParticipant && Contract is null)
                {
                    actions.Add(BookingAction.SignContract);
                }

                AddPaymentActions(actions, actor, hasBalance: true);

                if (isParticipant)
                {
                    actions.Add(BookingAction.Cancel);
                }

                break;

            case BookingStatus.Confirmed:
                AddPaymentActions(actions, actor, hasBalance);

                if (actor == Actor.Photographer || (actor == Actor.Client && ClientCanReschedule(now)))
                {
                    actions.Add(BookingAction.Reschedule);
                }

                if (isParticipant)
                {
                    actions.Add(BookingAction.Cancel);
                }

                if (actor == Actor.Photographer && now >= Slot.Start)
                {
                    actions.Add(BookingAction.Complete);
                }

                if (actor == Actor.Photographer && now >= ClientAbsentAllowedFrom)
                {
                    actions.Add(BookingAction.MarkClientAbsent);
                }

                break;

            case BookingStatus.Completed:
                AddPaymentActions(actions, actor, hasBalance);
                break;

            case BookingStatus.ClientAbsent:
                if (actor == Actor.Photographer && CanRevertClientAbsent(now))
                {
                    actions.Add(BookingAction.RevertClientAbsent);
                }

                break;

            case BookingStatus.Cancelled:
            case BookingStatus.Expired:
            default:
                break;
        }

        return actions;
    }

    /// <summary>Gets the earliest instant at which the client can be marked absent.</summary>
    private DateTimeOffset ClientAbsentAllowedFrom => Slot.Start.AddMinutes(Policy.ClientAbsentToleranceMinutes);

    /// <summary>
    /// Returns the channel used by an actor for actions that are not tied to a specific channel.
    /// </summary>
    /// <param name="actor">Actor performing the action.</param>
    /// <returns><see cref="Channel.Portal"/> for clients, <see cref="Channel.PhotographerApp"/> otherwise.</returns>
    private static Channel ChannelFor(Actor actor) => actor == Actor.Client ? Channel.Portal : Channel.PhotographerApp;

    /// <summary>
    /// Validates that a reason was provided.
    /// </summary>
    /// <param name="reason">Reason to validate.</param>
    /// <returns>The trimmed reason.</returns>
    /// <exception cref="DomainException">When the reason is empty.</exception>
    private static string RequireReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException(DomainErrorCodes.ReasonRequired, "A reason is required for this action.");
        }

        return reason.Trim();
    }

    /// <summary>
    /// Validates that the actor is one of the allowed actors.
    /// </summary>
    /// <param name="action">Name of the attempted action, for the error message.</param>
    /// <param name="actor">Actor performing the action.</param>
    /// <param name="allowed">Allowed actors.</param>
    /// <exception cref="DomainException">When the actor is not allowed.</exception>
    private static void EnsureActor(string action, Actor actor, params Actor[] allowed)
    {
        if (!allowed.Contains(actor))
        {
            throw new DomainException(DomainErrorCodes.ActorNotAllowed, $"{actor} cannot perform {action}.");
        }
    }

    /// <summary>
    /// Adds the payment-related actions available to an actor.
    /// </summary>
    /// <param name="actions">List to append to.</param>
    /// <param name="actor">Actor asking for the actions.</param>
    /// <param name="hasBalance">Whether there is an outstanding balance to pay.</param>
    private void AddPaymentActions(List<BookingAction> actions, Actor actor, bool hasBalance)
    {
        if (actor == Actor.Client && hasBalance)
        {
            actions.Add(BookingAction.SubmitPaymentProof);
        }

        if (actor == Actor.Photographer && HasPendingPaymentVerification)
        {
            actions.Add(BookingAction.VerifyPayment);
        }

        if (actor == Actor.Photographer && hasBalance)
        {
            actions.Add(BookingAction.RecordInPersonPayment);
        }
    }

    /// <summary>
    /// B6. Confirms the booking automatically when it is tentative, the contract is signed and the deposit is covered.
    /// </summary>
    /// <param name="now">Current instant.</param>
    private void TryConfirm(DateTimeOffset now)
    {
        if (Status != BookingStatus.Tentative || Contract is null || !IsDepositCovered)
        {
            return;
        }

        ExpiresAt = null;
        TransitionTo(BookingStatus.Confirmed, Actor.System, null, null, now);
        Raise(new BookingConfirmed(Id, PhotographerId, Slot.Start, now));
    }

    /// <summary>
    /// Indicates whether the client can still reschedule according to the copied policy.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> when the count and the notice of the policy allow it.</returns>
    private bool ClientCanReschedule(DateTimeOffset now) =>
        RescheduleCount < Policy.MaxReschedules
        && Slot.Start - now >= TimeSpan.FromHours(Policy.RescheduleMinNoticeHours);

    /// <summary>
    /// Indicates whether the client-absent mark can still be reverted.
    /// </summary>
    /// <param name="now">Current instant.</param>
    /// <returns><see langword="true"/> while the revert window of the policy is open.</returns>
    private bool CanRevertClientAbsent(DateTimeOffset now) =>
        ClientAbsentMarkedAt is not null && now <= ClientAbsentMarkedAt.Value.AddDays(Policy.ClientAbsentRevertWindowDays);

    /// <summary>
    /// Validates that the current status is one of the allowed statuses.
    /// </summary>
    /// <param name="action">Name of the attempted action, for the error message.</param>
    /// <param name="allowed">Statuses from which the action is allowed.</param>
    /// <exception cref="DomainException">When the current status is not allowed.</exception>
    private void EnsureStatus(string action, params BookingStatus[] allowed)
    {
        if (!allowed.Contains(Status))
        {
            throw new DomainException(DomainErrorCodes.InvalidTransition, $"{action} is not allowed while the booking is {Status}.");
        }
    }

    /// <summary>
    /// Validates that an amount is greater than zero and uses the currency of the booking.
    /// </summary>
    /// <param name="amount">Amount to validate.</param>
    /// <exception cref="DomainException">When the amount is zero or uses another currency.</exception>
    private void EnsurePositiveAmountInBookingCurrency(Money amount)
    {
        ArgumentNullException.ThrowIfNull(amount);

        if (!string.Equals(amount.Currency, PackagePrice.Currency, StringComparison.Ordinal))
        {
            throw new DomainException(DomainErrorCodes.CurrencyMismatch, $"Payments must be in {PackagePrice.Currency}.");
        }

        if (amount.Amount <= 0m)
        {
            throw new DomainException(DomainErrorCodes.InvalidAmount, "The payment amount must be greater than zero.");
        }
    }

    /// <summary>
    /// Finds a payment by idempotency key.
    /// </summary>
    /// <param name="idempotencyKey">Key to look for.</param>
    /// <returns>The matching payment, or <see langword="null"/>.</returns>
    /// <exception cref="DomainException">When the key is empty.</exception>
    private Payment? FindByIdempotencyKey(string idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "An idempotency key is required.");
        }

        var key = idempotencyKey.Trim();
        return _payments.Find(payment => string.Equals(payment.IdempotencyKey, key, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets a payment of the booking by identifier.
    /// </summary>
    /// <param name="paymentId">Payment identifier.</param>
    /// <returns>The payment.</returns>
    /// <exception cref="DomainException">When the payment does not belong to the booking.</exception>
    private Payment GetPayment(Guid paymentId) =>
        _payments.Find(payment => payment.Id == paymentId)
        ?? throw new DomainException(DomainErrorCodes.PaymentNotFound, $"Payment {paymentId} does not belong to booking {Id}.");

    /// <summary>
    /// Changes the status and appends the audit entry.
    /// </summary>
    /// <param name="to">New status.</param>
    /// <param name="actor">Who performs the transition.</param>
    /// <param name="channel">Channel of the transition.</param>
    /// <param name="reason">Optional reason.</param>
    /// <param name="now">Current instant.</param>
    private void TransitionTo(BookingStatus to, Actor actor, Channel? channel, string? reason, DateTimeOffset now)
    {
        _history.Add(new StatusTransition(Status, to, actor, channel, reason, now));
        Status = to;
    }
}
