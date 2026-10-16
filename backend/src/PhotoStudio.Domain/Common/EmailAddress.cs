namespace PhotoStudio.Domain.Common;

/// <summary>
/// Normalization of email addresses shared by every aggregate that stores one (users and the contact of a booking's client).
/// </summary>
public static class EmailAddress
{
    /// <summary>
    /// Longest accepted email (the limit of an address in the SMTP standard).
    /// </summary>
    public const int MaxLength = 254;

    /// <summary>
    /// Normalizes an email address: trimmed and in lower case, with one at sign, a name before it and a domain with a dot after
    /// it, no spaces, and at most <see cref="MaxLength"/> characters. It checks the shape only, not that the mailbox exists.
    /// </summary>
    /// <param name="email">Email typed by the user.</param>
    /// <returns>The normalized email, or <see langword="null"/> when it has no valid shape.</returns>
    public static string? TryNormalize(string? email)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized) || normalized.Length > MaxLength)
        {
            return null;
        }

        var at = normalized.IndexOf('@', StringComparison.Ordinal);
        var validShape = at > 0
            && at == normalized.LastIndexOf('@')
            && at < normalized.Length - 1
            && normalized.IndexOf('.', at + 1) > at + 1
            && normalized[^1] != '.'
            && !normalized.Any(char.IsWhiteSpace);

        return validShape ? normalized : null;
    }
}
