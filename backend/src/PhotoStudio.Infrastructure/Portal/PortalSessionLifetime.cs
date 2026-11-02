namespace PhotoStudio.Infrastructure.Portal;

/// <summary>
/// How long the session of a client lasts after they confirm the code that came to their email. The client confirms their email once
/// and the session is remembered in their browser, so it serves for the contract, the payments and the gallery for as long as it
/// lasts, instead of asking for a code for every action. The code still proves that the client owns the email the photographer
/// registered; the link remains the other half, since a session only opens the booking of that link.
/// </summary>
public static class PortalSessionLifetime
{
    /// <summary>Name of the environment variable that sets how many days a session lasts.</summary>
    public const string Variable = "PORTAL_SESSION_DAYS";

    /// <summary>Longest a session may last, in days.</summary>
    public const int MaxDays = 90;

    /// <summary>
    /// Gets how long a session lasts when nothing is configured: 30 days, the same as remembering a browser in the web area of the photographer.
    /// </summary>
    public static readonly TimeSpan Default = TimeSpan.FromDays(30);

    /// <summary>
    /// Reads how long a session lasts.
    /// </summary>
    /// <param name="value">Value of the environment variable, or <see langword="null"/>.</param>
    /// <returns>The lifetime: the default when the value is missing, otherwise the number of days it says.</returns>
    /// <exception cref="InvalidOperationException">When the value is not a whole number of days between 1 and 90.</exception>
    public static TimeSpan FromEnvironment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Default;
        }

        if (!int.TryParse(value.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var days) || days is < 1 or > MaxDays)
        {
            throw new InvalidOperationException($"'{Variable}' must be a whole number of days between 1 and {MaxDays}.");
        }

        return TimeSpan.FromDays(days);
    }
}
