using PhotoStudio.Application.Notifications.Push;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Notifications;

namespace PhotoStudio.Application.UnitTests.Notifications;

/// <summary>
/// Tests of <see cref="NotificationPushText"/>.
/// </summary>
public sealed class NotificationPushTextTests
{
    // 2026-10-17 is a Saturday; 21:00 UTC is 15:00 in Costa Rica (UTC-6).
    private static readonly DateTimeOffset SessionStart = new(2026, 10, 17, 21, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Builds a delivered notification of a type with the given details.
    /// </summary>
    /// <param name="type">Type of the notification.</param>
    /// <param name="balance">Amount owed, for balance notices.</param>
    /// <param name="proofAmount">Declared amount, for proof notices.</param>
    /// <returns>The delivered notification.</returns>
    private static Notification Delivered(NotificationType type, Money? balance = null, Money? proofAmount = null)
    {
        var notification = Notification.Schedule(Guid.CreateVersion7(), Guid.CreateVersion7(), type, $"{type}:key", SessionStart, SessionStart);
        notification.Deliver(new NotificationDetails("María Pérez", "+50688888888", "Retrato Familiar", SessionStart, balance, proofAmount), SessionStart);
        return notification;
    }

    /// <summary>
    /// A reminder names the client, the package and the session time in Costa Rica time.
    /// </summary>
    [Fact]
    public void Describe_ForAReminder_GivesTheSessionInCostaRicaTime()
    {
        var (title, body) = NotificationPushText.Describe(Delivered(NotificationType.SessionReminder));

        title.ShouldBe("Sesión próxima");
        body.ShouldBe("María Pérez · Retrato Familiar · sáb 17 oct, 3:00 p. m.");
    }

    /// <summary>
    /// A balance notice gives the amount owed.
    /// </summary>
    [Fact]
    public void Describe_ForABalance_GivesTheAmountOwed()
    {
        var (title, body) = NotificationPushText.Describe(Delivered(NotificationType.BalanceDue, balance: Money.Create(50_000m, "CRC")));

        title.ShouldBe("Saldo pendiente");
        body.ShouldBe("María Pérez debe ₡50 000 de su sesión de Retrato Familiar.");
    }

    /// <summary>
    /// A proof notice gives the declared amount.
    /// </summary>
    [Fact]
    public void Describe_ForAProof_GivesTheDeclaredAmount()
    {
        var (title, body) = NotificationPushText.Describe(Delivered(NotificationType.PaymentProofSubmitted, proofAmount: Money.Create(25_000m, "CRC")));

        title.ShouldBe("Comprobante por verificar");
        body.ShouldBe("María Pérez envió un comprobante de ₡25 000.");
    }

    /// <summary>
    /// A notification that was not delivered has nothing to say.
    /// </summary>
    [Fact]
    public void Describe_ForAnUndeliveredNotification_Fails()
    {
        var notification = Notification.Schedule(Guid.CreateVersion7(), Guid.CreateVersion7(), NotificationType.SessionReminder, "k", SessionStart, SessionStart);

        Should.Throw<InvalidOperationException>(() => NotificationPushText.Describe(notification));
    }

    /// <summary>
    /// Amounts use a space between thousands, a symbol for known currencies and the code for the others.
    /// </summary>
    /// <param name="amount">Amount.</param>
    /// <param name="currency">Currency code.</param>
    /// <param name="expected">Expected text.</param>
    [Theory]
    [InlineData(999, "CRC", "₡999")]
    [InlineData(1_250_000, "CRC", "₡1 250 000")]
    [InlineData(100, "USD", "US$100")]
    [InlineData(100, "EUR", "EUR 100")]
    public void FormatMoney_WritesTheAmountForTheCurrency(int amount, string currency, string expected) =>
        NotificationPushText.FormatMoney(Money.Create(amount, currency)).ShouldBe(expected);

    /// <summary>
    /// Midnight and noon are written as 12, and the hour changes the meridiem at noon.
    /// </summary>
    /// <param name="utcHour">Hour in UTC.</param>
    /// <param name="expected">Expected time text.</param>
    [Theory]
    [InlineData(6, "12:00 a. m.")]
    [InlineData(12, "6:00 a. m.")]
    [InlineData(18, "12:00 p. m.")]
    [InlineData(23, "5:00 p. m.")]
    public void FormatDateTime_UsesTwelveHoursInCostaRicaTime(int utcHour, string expected) =>
        NotificationPushText.FormatDateTime(new DateTimeOffset(2026, 10, 17, utcHour, 0, 0, TimeSpan.Zero)).ShouldEndWith(expected);

    /// <summary>
    /// A session late in the evening in Costa Rica falls on the next day in UTC; the text uses the Costa Rica day.
    /// </summary>
    [Fact]
    public void FormatDateTime_UsesTheCostaRicaDay()
    {
        // 2026-10-18 02:00 UTC is 2026-10-17 20:00 in Costa Rica.
        NotificationPushText.FormatDateTime(new DateTimeOffset(2026, 10, 18, 2, 0, 0, TimeSpan.Zero)).ShouldBe("sáb 17 oct, 8:00 p. m.");
    }
}
