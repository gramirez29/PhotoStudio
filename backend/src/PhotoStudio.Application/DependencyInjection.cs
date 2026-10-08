using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Bookings.CreateBooking;
using PhotoStudio.Application.Bookings.GetBooking;
using PhotoStudio.Application.Bookings.ListBookings;
using PhotoStudio.Application.Bookings.RecordInPersonPayment;
using PhotoStudio.Application.Bookings.RescheduleBooking;
using PhotoStudio.Application.Bookings.Responses;
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
        services.AddScoped<IQueryHandler<GetBookingQuery, BookingResponse>, GetBookingHandler>();
        services.AddScoped<IQueryHandler<ListBookingsQuery, IReadOnlyList<BookingSummaryResponse>>, ListBookingsHandler>();

        return services;
    }
}
