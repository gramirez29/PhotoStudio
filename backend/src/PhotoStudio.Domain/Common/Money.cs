namespace PhotoStudio.Domain.Common;

/// <summary>
/// Monetary amount in a specific ISO 4217 currency, rounded to two decimals.
/// </summary>
public sealed record Money
{
    /// <summary>
    /// Default currency of the platform (Costa Rican colón).
    /// </summary>
    public const string DefaultCurrency = "CRC";

    /// <summary>
    /// Initializes a new instance of the <see cref="Money"/> class. Use the static factory methods, which validate the values.
    /// </summary>
    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Gets the numeric amount. It is negative only as the result of a subtraction (for example, an overpaid balance).
    /// </summary>
    public decimal Amount { get; }

    /// <summary>
    /// Gets the three-letter ISO 4217 currency code in upper case.
    /// </summary>
    public string Currency { get; }

    /// <summary>
    /// Creates a non-negative amount.
    /// </summary>
    /// <param name="amount">Amount to represent; must be zero or greater.</param>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <returns>The validated amount, rounded to two decimals.</returns>
    /// <exception cref="DomainException">When the amount is negative or the currency is invalid.</exception>
    public static Money Create(decimal amount, string currency = DefaultCurrency)
    {
        if (amount < 0m)
        {
            throw new DomainException(DomainErrorCodes.InvalidAmount, "Amount cannot be negative.");
        }

        return new Money(Round(amount), NormalizeCurrency(currency));
    }

    /// <summary>
    /// Creates a zero amount in the given currency.
    /// </summary>
    /// <param name="currency">ISO 4217 currency code.</param>
    /// <returns>A zero amount.</returns>
    public static Money Zero(string currency = DefaultCurrency) => new(0m, NormalizeCurrency(currency));

    /// <summary>
    /// Rebuilds an amount from persisted data without the non-negative rule.
    /// </summary>
    /// <param name="amount">Persisted amount.</param>
    /// <param name="currency">Persisted currency code.</param>
    /// <returns>The restored amount.</returns>
    public static Money Restore(decimal amount, string currency) => new(amount, NormalizeCurrency(currency));

    /// <summary>
    /// Adds another amount of the same currency.
    /// </summary>
    /// <param name="other">Amount to add.</param>
    /// <returns>The sum.</returns>
    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount + other.Amount, Currency);
    }

    /// <summary>
    /// Subtracts another amount of the same currency. The result can be negative.
    /// </summary>
    /// <param name="other">Amount to subtract.</param>
    /// <returns>The difference.</returns>
    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return new Money(Amount - other.Amount, Currency);
    }

    /// <summary>
    /// Multiplies the amount by a factor and rounds the result to two decimals.
    /// </summary>
    /// <param name="factor">Multiplier, for example a deposit percentage expressed as a fraction.</param>
    /// <returns>The scaled amount.</returns>
    public Money MultiplyBy(decimal factor) => new(Round(Amount * factor), Currency);

    /// <summary>
    /// Indicates whether this amount is greater than or equal to another amount of the same currency.
    /// </summary>
    /// <param name="other">Amount to compare against.</param>
    /// <returns><see langword="true"/> when this amount is greater than or equal to <paramref name="other"/>.</returns>
    public bool IsGreaterThanOrEqualTo(Money other)
    {
        EnsureSameCurrency(other);
        return Amount >= other.Amount;
    }

    /// <summary>
    /// Rounds a value to two decimals using commercial rounding.
    /// </summary>
    /// <param name="value">Value to round.</param>
    /// <returns>The rounded value.</returns>
    private static decimal Round(decimal value) => decimal.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Validates and normalizes a currency code.
    /// </summary>
    /// <param name="currency">Currency code to normalize.</param>
    /// <returns>The trimmed, upper-case code.</returns>
    /// <exception cref="DomainException">When the code is not three letters long.</exception>
    private static string NormalizeCurrency(string currency)
    {
        var normalized = currency?.Trim().ToUpperInvariant() ?? string.Empty;
        if (normalized.Length != 3 || !normalized.All(char.IsAsciiLetterUpper))
        {
            throw new DomainException(DomainErrorCodes.InvalidCurrency, $"'{currency}' is not a valid ISO 4217 currency code.");
        }

        return normalized;
    }

    /// <summary>
    /// Ensures another amount uses the same currency.
    /// </summary>
    /// <param name="other">Amount to check.</param>
    /// <exception cref="DomainException">When the currencies differ.</exception>
    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.Ordinal))
        {
            throw new DomainException(DomainErrorCodes.CurrencyMismatch, $"Cannot combine {Currency} with {other.Currency}.");
        }
    }
}
