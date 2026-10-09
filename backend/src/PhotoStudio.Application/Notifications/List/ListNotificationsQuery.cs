namespace PhotoStudio.Application.Notifications.List;

/// <summary>
/// Query for the inbox of a photographer.
/// </summary>
/// <param name="PhotographerId">Authenticated photographer.</param>
public sealed record ListNotificationsQuery(Guid PhotographerId);
