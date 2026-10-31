using System.Globalization;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.Notifications.Push;

/// <summary>
/// The words of a push notification. This is the one place where the backend holds user-facing text: the inbox is worded by
/// the app, but a push is drawn by the operating system while the app is closed, so its text has to travel with it. The
/// product is Spanish only (Costa Rica), so the text is fixed here. Dates are written without relying on the culture data
/// of the host, which a slim container image may lack.
/// </summary>
public static class NotificationPushText
{
    /// <summary>
    /// Costa Rica has no daylight saving time, so a fixed offset is exact all year.
    /// </summary>
    private static readonly TimeSpan CostaRicaOffset = TimeSpan.FromHours(-6);

    private static readonly string[] Days = ["dom", "lun", "mar", "mié", "jue", "vie", "sáb"];

    private static readonly string[] Months = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "set", "oct", "nov", "dic"];

    /// <summary>
    /// Words a delivered notification as a push.
    /// </summary>
    /// <param name="notification">A delivered notification, so it has details.</param>
    /// <returns>The title and the body.</returns>
    /// <exception cref="InvalidOperationException">When the notification was not delivered and has nothing to say.</exception>
    public static (string Title, string Body) Describe(Notification notification)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var details = notification.Details
            ?? throw new InvalidOperationException($"Notification {notification.Id} was not delivered, so it has nothing to say.");

        switch (notification.Type)
        {
            case NotificationType.BalanceDue when details.Balance is not null:
                return ("Saldo pendiente", $"{details.ClientName} debe {FormatMoney(details.Balance)} de su sesión de {details.PackageName}.");

            case NotificationType.PaymentProofSubmitted when details.ProofAmount is not null:
                return ("Comprobante por verificar", $"{details.ClientName} envió un comprobante de {FormatMoney(details.ProofAmount)}.");

            case NotificationType.SelectionSubmitted when details.SelectedPhotos is { } selected:
                var extra = details.ExtraPhotos is > 0 ? $" ({details.ExtraPhotos} extra)" : string.Empty;
                return ("Selección recibida", $"{details.ClientName} eligió {selected} {(selected == 1 ? "foto" : "fotos")}{extra}.");

            default:
                return ("Sesión próxima", $"{details.ClientName} · {details.PackageName} · {FormatDateTime(details.SessionStart)}");
        }
    }

    /// <summary>
    /// Writes an amount the way the app does: colones with their symbol and thousands separated by a space.
    /// </summary>
    /// <param name="money">Amount to write.</param>
    /// <returns>For example <c>₡25 000</c>; a currency without a symbol is written after its code.</returns>
    internal static string FormatMoney(Money money)
    {
        var digits = decimal.Round(money.Amount, 0, MidpointRounding.AwayFromZero)
            .ToString("N0", CultureInfo.InvariantCulture)
            .Replace(',', ' ');

        return money.Currency switch
        {
            "CRC" => $"₡{digits}",
            "USD" => $"US${digits}",
            _ => $"{money.Currency} {digits}",
        };
    }

    /// <summary>
    /// Writes an instant in Costa Rica time, for example <c>vie 17 oct, 3:00 p. m.</c>.
    /// </summary>
    /// <param name="instant">Instant to write.</param>
    /// <returns>The text.</returns>
    internal static string FormatDateTime(DateTimeOffset instant)
    {
        var local = instant.ToOffset(CostaRicaOffset);
        var hour = local.Hour % 12 == 0 ? 12 : local.Hour % 12;
        var meridiem = local.Hour < 12 ? "a. m." : "p. m.";
        return $"{Days[(int)local.DayOfWeek]} {local.Day} {Months[local.Month - 1]}, {hour}:{local.Minute:00} {meridiem}";
    }
}
