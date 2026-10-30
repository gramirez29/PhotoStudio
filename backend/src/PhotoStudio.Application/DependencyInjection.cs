using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Billing;
using PhotoStudio.Application.Billing.CompleteRefund;
using PhotoStudio.Application.Billing.GetSettlement;
using PhotoStudio.Application.Billing.ListRefunds;
using PhotoStudio.Application.Bookings.CancelBooking;
using PhotoStudio.Application.Bookings.CompleteBooking;
using PhotoStudio.Application.Bookings.CreateBooking;
using PhotoStudio.Application.Bookings.EditBookingDetails;
using PhotoStudio.Application.Bookings.ExpireTentativeBookings;
using PhotoStudio.Application.Bookings.GetContract;
using PhotoStudio.Application.Bookings.GetBooking;
using PhotoStudio.Application.Bookings.ListBookings;
using PhotoStudio.Application.Galleries;
using PhotoStudio.Application.Galleries.EditTerms;
using PhotoStudio.Application.Galleries.GetGallery;
using PhotoStudio.Application.Galleries.ListGalleries;
using PhotoStudio.Application.Contracts;
using PhotoStudio.Application.Identity;
using PhotoStudio.Application.Identity.Studio;
using PhotoStudio.Application.Identity.ChangePassword;
using PhotoStudio.Application.Identity.Login;
using PhotoStudio.Application.Identity.Logout;
using PhotoStudio.Application.Identity.RefreshSession;
using PhotoStudio.Application.Identity.ResendEmailVerification;
using PhotoStudio.Application.Identity.Register;
using PhotoStudio.Application.Identity.VerifyEmail;
using PhotoStudio.Application.Maintenance.RunMaintenance;
using PhotoStudio.Application.Notifications;
using PhotoStudio.Application.Notifications.DeliverDue;
using PhotoStudio.Application.Notifications.List;
using PhotoStudio.Application.Notifications.MarkRead;
using PhotoStudio.Application.Notifications.Push;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Application.Bookings.MarkClientAbsent;
using PhotoStudio.Application.Bookings.RecordInPersonPayment;
using PhotoStudio.Application.Bookings.RescheduleBooking;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Bookings.RevertClientAbsent;
using PhotoStudio.Application.Bookings.SignContractInPerson;
using PhotoStudio.Application.Bookings.RejectPayment;
using PhotoStudio.Application.Bookings.VerifyPayment;
using PhotoStudio.Application.Portal;
using PhotoStudio.Application.Portal.CreateLink;
using PhotoStudio.Application.Portal.GetBooking;
using PhotoStudio.Application.Portal.GetContract;
using PhotoStudio.Application.Portal.RequestCode;
using PhotoStudio.Application.Portal.SignContract;
using PhotoStudio.Application.Portal.SubmitProof;
using PhotoStudio.Application.Portal.VerifyCode;

namespace PhotoStudio.Application;

/// <summary>
/// Registers the application layer in the dependency injection container.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Adds every command and query handler, plus the system clock when none was registered.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<ICommandHandler<CreateBookingCommand, BookingResponse>, CreateBookingHandler>();
        services.AddScoped<ICommandHandler<SignContractInPersonCommand, BookingResponse>, SignContractInPersonHandler>();
        services.AddScoped<ICommandHandler<RecordInPersonPaymentCommand, BookingResponse>, RecordInPersonPaymentHandler>();
        services.AddScoped<ICommandHandler<VerifyPaymentCommand, BookingResponse>, VerifyPaymentHandler>();
        services.AddScoped<ICommandHandler<RejectPaymentCommand, BookingResponse>, RejectPaymentHandler>();
        services.AddScoped<ICommandHandler<RescheduleBookingCommand, BookingResponse>, RescheduleBookingHandler>();
        services.AddScoped<ICommandHandler<CancelBookingCommand, BookingResponse>, CancelBookingHandler>();
        services.AddScoped<ICommandHandler<CompleteBookingCommand, BookingResponse>, CompleteBookingHandler>();
        services.AddScoped<ICommandHandler<MarkClientAbsentCommand, BookingResponse>, MarkClientAbsentHandler>();
        services.AddScoped<ICommandHandler<RevertClientAbsentCommand, BookingResponse>, RevertClientAbsentHandler>();
        services.AddScoped<ICommandHandler<ExpireTentativeBookingsCommand, ExpireTentativeBookingsResult>, ExpireTentativeBookingsHandler>();
        services.AddScoped<ICommandHandler<RunMaintenanceCommand, MaintenanceResponse>, RunMaintenanceHandler>();
        services.AddScoped<ICommandHandler<DeliverDueNotificationsCommand, DeliverDueNotificationsResult>, DeliverDueNotificationsHandler>();
        services.AddScoped<ICommandHandler<MarkNotificationReadCommand, NotificationResponse>, MarkNotificationReadHandler>();
        services.AddScoped<ICommandHandler<MarkAllNotificationsReadCommand, MarkAllReadResponse>, MarkAllNotificationsReadHandler>();
        services.AddScoped<IQueryHandler<ListNotificationsQuery, NotificationListResponse>, ListNotificationsHandler>();

        // Consumers of the outbox: they keep the notifications of a booking in line with it.
        services.AddScoped<ICommandHandler<CompleteRefundCommand, SettlementResponse>, CompleteRefundHandler>();
        services.AddScoped<IQueryHandler<GetSettlementQuery, SettlementResponse>, GetSettlementHandler>();
        services.AddScoped<IQueryHandler<ListRefundsQuery, RefundListResponse>, ListRefundsHandler>();

        // Billing: the money of a booking that ended is settled from the outbox events; the planner works from the current state.
        services.AddScoped<SettlementPlanner>();
        services.AddScoped<IDomainEventHandler<BookingCancelled>, BookingSettlementHandler>();
        services.AddScoped<IDomainEventHandler<BookingExpired>, BookingSettlementHandler>();
        services.AddScoped<IDomainEventHandler<ClientMarkedAbsent>, BookingSettlementHandler>();
        services.AddScoped<IDomainEventHandler<ClientAbsenceReverted>, BookingSettlementHandler>();

        services.TryAddSingleton<IOutboxWakeUp, NullOutboxWakeUp>();
        services.AddScoped<NotificationPusher>();
        services.AddScoped<ICommandHandler<RegisterPushDeviceCommand, bool>, RegisterPushDeviceHandler>();
        services.AddScoped<ICommandHandler<UnregisterPushDeviceCommand, bool>, UnregisterPushDeviceHandler>();

        services.AddScoped<NotificationPlanner>();
        services.AddScoped<IDomainEventHandler<BookingConfirmed>, BookingNotificationsHandler>();
        services.AddScoped<IDomainEventHandler<BookingRescheduled>, BookingNotificationsHandler>();
        services.AddScoped<IDomainEventHandler<BookingCancelled>, BookingNotificationsHandler>();
        services.AddScoped<IDomainEventHandler<BookingExpired>, BookingNotificationsHandler>();
        services.AddScoped<IDomainEventHandler<ClientMarkedAbsent>, BookingNotificationsHandler>();
        services.AddScoped<IDomainEventHandler<SessionCompleted>, SessionCompletedNotificationsHandler>();
        services.AddScoped<IDomainEventHandler<PaymentSubmitted>, PaymentNotificationsHandler>();

        // Contracts: the client gets an email copy of the contract they signed through the portal.
        services.AddScoped<IDomainEventHandler<ContractSigned>, ContractCopyHandler>();

        services.AddScoped<ICommandHandler<EditBookingDetailsCommand, BookingResponse>, EditBookingDetailsHandler>();
        services.AddScoped<IQueryHandler<GetBookingQuery, BookingResponse>, GetBookingHandler>();
        services.AddScoped<IQueryHandler<GetBookingContractQuery, BookingContractResponse>, GetBookingContractHandler>();
        services.AddScoped<IQueryHandler<ListBookingsQuery, BookingListResponse>, ListBookingsHandler>();

        // Galleries: created by the system when a session is completed, read and adjusted by the photographer.
        services.AddScoped<IDomainEventHandler<SessionCompleted>, GalleryCreationHandler>();
        services.AddScoped<IQueryHandler<ListGalleriesQuery, GalleryListResponse>, ListGalleriesHandler>();
        services.AddScoped<IQueryHandler<GetGalleryQuery, GalleryResponse>, GetGalleryHandler>();
        services.AddScoped<IQueryHandler<GetGalleryByBookingQuery, GalleryResponse>, GetGalleryByBookingHandler>();
        services.AddScoped<ICommandHandler<EditGalleryTermsCommand, GalleryResponse>, EditGalleryTermsHandler>();

        return services;
    }

    /// <summary>
    /// Adds the use cases of the client portal: creating the booking link (photographer) and everything the client does through
    /// it. Registered apart from <see cref="AddApplication"/> because they need the email, token and portal settings that only
    /// the API configures.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApplicationPortal(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICommandHandler<CreateBookingLinkCommand, BookingLinkResponse>, CreateBookingLinkHandler>();
        services.AddScoped<IQueryHandler<GetPortalBookingQuery, PortalBookingResponse>, GetPortalBookingHandler>();
        services.AddScoped<ICommandHandler<RequestPortalCodeCommand, PortalCodeSentResponse>, RequestPortalCodeHandler>();
        services.AddScoped<ICommandHandler<VerifyPortalCodeCommand, PortalSessionResponse>, VerifyPortalCodeHandler>();
        services.AddScoped<ICommandHandler<SubmitPortalPaymentProofCommand, PortalBookingResponse>, SubmitPortalPaymentProofHandler>();
        services.AddScoped<IQueryHandler<GetPortalContractQuery, PortalContractResponse>, GetPortalContractHandler>();
        services.AddScoped<ICommandHandler<SignPortalContractCommand, PortalBookingResponse>, SignPortalContractHandler>();

        return services;
    }

    /// <summary>
    /// Adds the authentication use cases (register, login, refresh, logout and change password). Registered apart from
    /// <see cref="AddApplication"/> because they need the token ports, which only the API configures.
    /// </summary>
    /// <param name="services">Service collection to configure.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddApplicationAuthentication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<SessionIssuer>();
        services.AddScoped<ICommandHandler<LoginCommand, AuthSessionResponse>, LoginHandler>();
        services.AddScoped<ICommandHandler<RefreshSessionCommand, AuthSessionResponse>, RefreshSessionHandler>();
        services.AddScoped<ICommandHandler<LogoutCommand, LogoutResponse>, LogoutHandler>();
        services.AddScoped<ICommandHandler<RegisterUserCommand, AuthSessionResponse>, RegisterUserHandler>();
        services.AddScoped<ICommandHandler<ChangePasswordCommand, AuthSessionResponse>, ChangePasswordHandler>();
        services.AddScoped<EmailVerificationSender>();
        services.AddScoped<ICommandHandler<VerifyEmailCommand, AuthSessionResponse>, VerifyEmailHandler>();
        services.AddScoped<ICommandHandler<ResendEmailVerificationCommand, EmailVerificationSentResponse>, ResendEmailVerificationHandler>();

        // The web area of the photographer: password plus a code by email in a browser that is not remembered.
        services.AddScoped<CredentialChecker>();
        services.AddScoped<StudioSessionIssuer>();
        services.AddScoped<StudioAccessRevoker>();
        services.AddScoped<StudioSessionValidator>();
        services.AddScoped<ICommandHandler<StudioLoginCommand, StudioLoginResult>, StudioLoginHandler>();
        services.AddScoped<ICommandHandler<StudioVerifyCodeCommand, StudioVerifyResult>, StudioVerifyCodeHandler>();
        services.AddScoped<ICommandHandler<StudioResendCodeCommand, StudioCodeSentResponse>, StudioResendCodeHandler>();
        services.AddScoped<ICommandHandler<StudioLogoutCommand, bool>, StudioLogoutHandler>();

        return services;
    }
}
