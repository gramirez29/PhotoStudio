using System.Globalization;
using System.Text.RegularExpressions;
using PhotoStudio.Application.Notifications.Push;
using PhotoStudio.Domain.Bookings;

namespace PhotoStudio.Application.Contracts;

/// <summary>
/// Fills the variables of a contract template with the data of one booking. The policy numbers (hours to hold the date, notice
/// to reschedule, free cancellation window...) come from the policy frozen in the booking, so the contract always says what the
/// system will do for that booking, even if the photographer's defaults change later. What only the client knows (identity
/// number, number of people, location, image use) and who signs stay as blanks until they are given.
/// </summary>
public static partial class ContractRenderer
{
    /// <summary>What is written where a value is not known yet.</summary>
    public const string Blank = "________";

    /// <summary>Costa Rica has no daylight saving time, so a fixed offset is exact all year.</summary>
    private static readonly TimeSpan CostaRicaOffset = TimeSpan.FromHours(-6);

    private static readonly string[] Days = ["domingo", "lunes", "martes", "miércoles", "jueves", "viernes", "sábado"];

    private static readonly string[] Months =
        ["enero", "febrero", "marzo", "abril", "mayo", "junio", "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"];

    /// <summary>
    /// Writes the contract of a booking.
    /// </summary>
    /// <param name="template">Template text with <c>{{name}}</c> variables.</param>
    /// <param name="booking">Booking the contract is for.</param>
    /// <param name="declaration">What the client declared, or <see langword="null"/> to leave those blanks empty.</param>
    /// <param name="signerName">Name typed by the signer, or <see langword="null"/> before signing.</param>
    /// <param name="signedAt">Instant of the signature, or <see langword="null"/> before signing.</param>
    /// <returns>The text of the contract.</returns>
    /// <exception cref="InvalidOperationException">When the template uses a variable that does not exist.</exception>
    public static string Render(string template, Booking booking, ClientDeclaration? declaration, string? signerName, DateTimeOffset? signedAt)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(booking);

        var values = BuildValues(booking, declaration, signerName, signedAt);
        return Variable().Replace(template, match =>
            values.TryGetValue(match.Groups[1].Value, out var value)
                ? value
                : throw new InvalidOperationException($"The contract template uses an unknown variable: {match.Value}."));
    }

    /// <summary>
    /// Lists the variables a template can use.
    /// </summary>
    /// <param name="booking">Any booking; the names do not depend on it.</param>
    /// <returns>The variable names, without braces.</returns>
    internal static IReadOnlyCollection<string> KnownVariables(Booking booking) => BuildValues(booking, null, null, null).Keys;

    /// <summary>
    /// Finds the variables a template uses.
    /// </summary>
    /// <param name="template">Template text.</param>
    /// <returns>The distinct variable names, without braces.</returns>
    internal static IReadOnlyCollection<string> UsedVariables(string template) =>
        [.. Variable().Matches(template).Select(match => match.Groups[1].Value).Distinct()];

    /// <summary>
    /// Builds the value of every variable for one booking.
    /// </summary>
    /// <param name="booking">Booking the contract is for.</param>
    /// <param name="declaration">What the client declared, if given.</param>
    /// <param name="signerName">Name typed by the signer, if given.</param>
    /// <param name="signedAt">Instant of the signature, if given.</param>
    /// <returns>The values by variable name.</returns>
    private static Dictionary<string, string> BuildValues(Booking booking, ClientDeclaration? declaration, string? signerName, DateTimeOffset? signedAt)
    {
        var policy = booking.Policy;
        var depositPercent = decimal.Round(policy.DepositPercentage * 100m, 2);
        var start = booking.Slot.Start.ToOffset(CostaRicaOffset);

        return new Dictionary<string, string>
        {
            ["clientName"] = booking.Client.Name,
            ["idNumber"] = declaration?.IdNumber ?? Blank,
            ["phone"] = FormatPhone(booking.Client.Phone),
            ["service"] = booking.PackageName,
            ["packageText"] = FormatPackage(booking),
            ["extrasText"] = FormatExtras(booking),
            ["priceBreakdown"] = FormatPriceBreakdown(booking),
            ["sessionDate"] = FormatLongDate(start),
            ["sessionTime"] = FormatTime(start),
            ["location"] = declaration is null ? Blank : declaration.SessionLocation ?? "un lugar que se definirá con el Fotógrafo",
            ["participants"] = declaration is null ? Blank : declaration.ParticipantCount.ToString(CultureInfo.InvariantCulture),
            ["totalPrice"] = NotificationPushText.FormatMoney(booking.PackagePrice),
            ["depositPercent"] = FormatNumber(depositPercent),
            ["depositAmount"] = NotificationPushText.FormatMoney(booking.DepositRequired),
            ["balancePercent"] = FormatNumber(100m - depositPercent),
            ["balanceAmount"] = NotificationPushText.FormatMoney(booking.PackagePrice.Subtract(booking.DepositRequired)),
            ["holdHours"] = policy.TentativeHoldHours.ToString(CultureInfo.InvariantCulture),
            ["rescheduleNoticeHours"] = policy.RescheduleMinNoticeHours.ToString(CultureInfo.InvariantCulture),
            ["rescheduleLimit"] = FormatRescheduleLimit(policy.MaxReschedules),
            ["freeCancellationHours"] = policy.FreeCancellationWindowHours.ToString(CultureInfo.InvariantCulture),
            ["absentToleranceMinutes"] = policy.ClientAbsentToleranceMinutes.ToString(CultureInfo.InvariantCulture),
            ["imageAuthorized"] = declaration?.ImageUse == ImageUse.Authorized ? "[x]" : "[ ]",
            ["imageDenied"] = declaration?.ImageUse == ImageUse.NotAuthorized ? "[x]" : "[ ]",
            ["signedDate"] = signedAt is { } instant ? FormatLongDate(instant.ToOffset(CostaRicaOffset)) + ", " + FormatTime(instant.ToOffset(CostaRicaOffset)) : Blank,
            ["signerName"] = string.IsNullOrWhiteSpace(signerName) ? Blank : signerName.Trim(),
        };
    }

    /// <summary>
    /// Writes the package the client booked, to follow the type of session.
    /// </summary>
    /// <param name="booking">Booking the contract is for.</param>
    /// <returns>For example <c>, con el paquete Estándar</c>; empty when the booking has no package.</returns>
    private static string FormatPackage(Booking booking) => booking.PackageTier switch
    {
        PackageTier.Basic => ", con el paquete Básico",
        PackageTier.Standard => ", con el paquete Estándar",
        PackageTier.Premium => ", con el paquete Premium",
        PackageTier.Custom => ", con un paquete personalizado acordado con el Cliente",
        PackageTier.Other => ", con las condiciones acordadas con el Cliente",
        _ => string.Empty,
    };

    /// <summary>
    /// Lists the extras sold with the booking.
    /// </summary>
    /// <param name="booking">Booking the contract is for.</param>
    /// <returns>For example <c> Extras contratados: Hora adicional × 2 (₡60 000).</c>; empty when there are none.</returns>
    private static string FormatExtras(Booking booking) => booking.Extras.Count == 0
        ? string.Empty
        : " Extras contratados: " + string.Join(", ", booking.Extras.Select(extra =>
            $"{extra.Name} × {extra.Quantity.ToString(CultureInfo.InvariantCulture)} ({NotificationPushText.FormatMoney(extra.Total)})")) + ".";

    /// <summary>
    /// Shows how the total is made up when it is not just the package.
    /// </summary>
    /// <param name="booking">Booking the contract is for.</param>
    /// <returns>For example <c> (paquete ₡65 000, extras ₡60 000, descuento ₡5 000)</c>; empty when the total is the package alone.</returns>
    private static string FormatPriceBreakdown(Booking booking)
    {
        if (booking.Extras.Count == 0 && booking.Discount.Amount == 0m)
        {
            return string.Empty;
        }

        var parts = new List<string> { $"paquete {NotificationPushText.FormatMoney(booking.BasePrice)}" };
        if (booking.Extras.Count > 0)
        {
            parts.Add($"extras {NotificationPushText.FormatMoney(booking.ExtrasTotal)}");
        }

        if (booking.Discount.Amount > 0m)
        {
            parts.Add($"descuento -{NotificationPushText.FormatMoney(booking.Discount)}");
        }

        return $" ({string.Join(", ", parts)})";
    }

    /// <summary>
    /// Writes how many times the client may reschedule.
    /// </summary>
    /// <param name="maxReschedules">Maximum number of reschedules the policy allows the client.</param>
    /// <returns>For example <c>una sola vez</c> or <c>hasta 2 veces</c>.</returns>
    private static string FormatRescheduleLimit(int maxReschedules) => maxReschedules switch
    {
        <= 0 => "ninguna vez",
        1 => "una sola vez",
        _ => $"hasta {maxReschedules.ToString(CultureInfo.InvariantCulture)} veces",
    };

    /// <summary>
    /// Writes a number without trailing zeros, for example <c>50</c> or <c>33.33</c>.
    /// </summary>
    /// <param name="value">Number to write.</param>
    /// <returns>The text.</returns>
    private static string FormatNumber(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes a Costa Rican phone as <c>7018-9220</c>; any other shape is left as it was saved.
    /// </summary>
    /// <param name="phone">Phone as saved, for example <c>+50670189220</c>.</param>
    /// <returns>The text.</returns>
    private static string FormatPhone(string phone)
    {
        const string countryCode = "+506";
        if (phone.StartsWith(countryCode, StringComparison.Ordinal) && phone.Length == countryCode.Length + 8 && phone[countryCode.Length..].All(char.IsAsciiDigit))
        {
            var local = phone[countryCode.Length..];
            return $"{local[..4]}-{local[4..]}";
        }

        return phone;
    }

    /// <summary>
    /// Writes a date in words without relying on the culture data of the host, for example <c>viernes 17 de octubre de 2026</c>.
    /// </summary>
    /// <param name="local">Instant in Costa Rica time.</param>
    /// <returns>The text.</returns>
    private static string FormatLongDate(DateTimeOffset local) =>
        $"{Days[(int)local.DayOfWeek]} {local.Day} de {Months[local.Month - 1]} de {local.Year}";

    /// <summary>
    /// Writes a time of day, for example <c>3:00 p. m.</c>.
    /// </summary>
    /// <param name="local">Instant in Costa Rica time.</param>
    /// <returns>The text.</returns>
    private static string FormatTime(DateTimeOffset local)
    {
        var hour = local.Hour % 12 == 0 ? 12 : local.Hour % 12;
        return $"{hour}:{local.Minute:00} {(local.Hour < 12 ? "a. m." : "p. m.")}";
    }

    /// <summary>
    /// Matches a <c>{{name}}</c> variable of a template.
    /// </summary>
    /// <returns>The compiled expression.</returns>
    [GeneratedRegex(@"\{\{(\w+)\}\}")]
    private static partial Regex Variable();
}
