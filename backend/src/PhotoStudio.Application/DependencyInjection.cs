using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.CancelBooking;
using PhotoStudio.Application.Bookings.CompleteBooking;
using PhotoStudio.Application.Bookings.CreateBooking;
using PhotoStudio.Application.Bookings.ExpireTentativeBookings;
using PhotoStudio.Application.Bookings.GetBooking;
using PhotoStudio.Application.Bookings.ListBookings;
using PhotoStudio.Application.Identity;
using PhotoStudio.Application.Identity.Login;
using PhotoStudio.Application.Identity.Logout;
using PhotoStudio.Application.Identity.RefreshSession;
using PhotoStudio.Application.Identity.Register;
using PhotoStudio.Application.Maintenance.RunMaintenance;
using PhotoStudio.Application.Notifications;
using PhotoStudio.Application.Notifications.DeliverDue;
using PhotoStudio.Application.Notifications.List;
using PhotoStudio.Application.Notifications.MarkRead;
using PhotoStudio.Domain.Bookings.Events;
using PhotoStudio.Application.Bookings.MarkClientAbsent;
using PhotoStudio.Application.Bookings.RecordInPersonPayment;
using PhotoStudio.Application.Bookings.RescheduleBooking;
using PhotoStudio.Application.Bookings.Responses;
using PhotoStudio.Application.Bookings.RevertClientAbsent;
using PhotoStudio.Application.Bookings.SignContractInPerson;

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
        services.AddScoped<NotificationPlanner>();
        services.AddScoped<IDomainEventHandler<BookingConfirmed>, BookingNotificationsHandler>();
        services.AddScoped<IDomainEventHandler<BookingRescheduled>, BookingNotificationsHandler>();
        services.AddScoped<IDomainEventHandler<BookingCancelled>, BookingNotificationsHandler>();
        services.AddScoped<IDomainEventHandler<BookingExpired>, BookingNotificationsHandler>();
        services.AddScoped<IDomainEventHandler<ClientMarkedAbsent>, BookingNotificationsHandler>();
        services.AddScoped<IDomainEventHandler<SessionCompleted>, SessionCompletedNotificationsHandler>();

        services.AddScoped<IQueryHandler<GetBookingQuery, BookingResponse>, GetBookingHandler>();
        services.AddScoped<IQueryHandler<ListBookingsQuery, IReadOnlyList<BookingSummaryResponse>>, ListBookingsHandler>();

        return services;
    }

    /// <summary>
    /// Adds the authentication use cases (register, login, refresh and logout). Registered apart from
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

        return services;
    }
}
