using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using PhotoStudio.Api.Auth;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Notifications;
using PhotoStudio.Application.Notifications.List;
using PhotoStudio.Application.Notifications.MarkRead;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// The inbox of the signed-in photographer: notices about their bookings that the background job delivers (a reminder for a
/// session that is about to start, a balance that is still owed). Every operation is scoped to the photographer of the token.
/// </summary>
public static class NotificationEndpoints
{
    /// <summary>
    /// Maps the notification endpoints under <c>/api/notifications</c>.
    /// </summary>
    /// <param name="endpoints">Route builder.</param>
    /// <returns>The same route builder, for chaining.</returns>
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        var group = endpoints.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization();

        group.MapGet("/", ListAsync).WithName("ListNotifications");
        group.MapPost("/read-all", MarkAllReadAsync).WithName("MarkAllNotificationsRead");
        group.MapPost("/{id:guid}/read", MarkReadAsync).WithName("MarkNotificationRead");

        return endpoints;
    }

    /// <summary>
    /// Lists the inbox: the latest delivered notifications and how many are unread.
    /// </summary>
    /// <param name="user">Authenticated photographer.</param>
    /// <param name="handler">Query handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the inbox.</returns>
    private static async Task<Ok<NotificationListResponse>> ListAsync(
        ClaimsPrincipal user,
        IQueryHandler<ListNotificationsQuery, NotificationListResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new ListNotificationsQuery(user.GetPhotographerId()), cancellationToken));

    /// <summary>
    /// Marks one notification as read.
    /// </summary>
    /// <param name="id">Notification identifier.</param>
    /// <param name="user">Authenticated photographer; the notification must be theirs, otherwise the answer is 404.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with the notification.</returns>
    private static async Task<Ok<NotificationResponse>> MarkReadAsync(
        Guid id,
        ClaimsPrincipal user,
        ICommandHandler<MarkNotificationReadCommand, NotificationResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new MarkNotificationReadCommand(user.GetPhotographerId(), id), cancellationToken));

    /// <summary>
    /// Marks every unread notification of the photographer as read.
    /// </summary>
    /// <param name="user">Authenticated photographer.</param>
    /// <param name="handler">Command handler.</param>
    /// <param name="cancellationToken">Request cancellation token.</param>
    /// <returns>200 with how many were unread.</returns>
    private static async Task<Ok<MarkAllReadResponse>> MarkAllReadAsync(
        ClaimsPrincipal user,
        ICommandHandler<MarkAllNotificationsReadCommand, MarkAllReadResponse> handler,
        CancellationToken cancellationToken) =>
        TypedResults.Ok(await handler.HandleAsync(new MarkAllNotificationsReadCommand(user.GetPhotographerId()), cancellationToken));
}
