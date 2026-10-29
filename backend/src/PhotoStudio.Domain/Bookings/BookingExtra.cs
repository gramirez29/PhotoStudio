using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Bookings;

/// <summary>
/// An extra sold on top of the package (additional hour, additional person...). The unit price is a snapshot taken when the
/// booking was made, so changing the prices of the photographer later never changes an existing booking.
/// </summary>
public sealed record BookingExtra
{
    /// <summary>Largest quantity of a single extra.</summary>
    public const int MaxQuantity = 99;

    /// <summary>Longest code of an extra.</summary>
    public const int MaxCodeLength = 40;

    /// <summary>Longest name of an extra.</summary>
    public const int MaxNameLength = 80;

    /// <summary>
    /// Initializes a new instance of the <see cref="BookingExtra"/> class. Use <see cref="Create"/>.
    /// </summary>
    /// <param name="code">Stable code of the extra.</param>
    /// <param name="name">Name of the extra.</param>
    /// <param name="unitPrice">Price of one unit.</param>
    /// <param name="quantity">Number of units.</param>
    private BookingExtra(string code, string name, Money unitPrice, int quantity)
    {
        Code = code;
        Name = name;
        UnitPrice = unitPrice;
        Quantity = quantity;
    }

    /// <summary>Gets the stable code of the extra, used to tell two extras apart.</summary>
    public string Code { get; }

    /// <summary>Gets the name of the extra, as the photographer saw it when selling it.</summary>
    public string Name { get; }

    /// <summary>Gets the price of one unit when the booking was made.</summary>
    public Money UnitPrice { get; }

    /// <summary>Gets the number of units sold.</summary>
    public int Quantity { get; }

    /// <summary>Gets the price of every unit together.</summary>
    public Money Total => UnitPrice.MultiplyBy(Quantity);

    /// <summary>
    /// Creates an extra after validating its values.
    /// </summary>
    /// <param name="code">Stable code of the extra.</param>
    /// <param name="name">Name of the extra.</param>
    /// <param name="unitPrice">Price of one unit.</param>
    /// <param name="quantity">Number of units, from 1 to <see cref="MaxQuantity"/>.</param>
    /// <returns>The extra.</returns>
    /// <exception cref="DomainException">When a value is missing, too long or out of range.</exception>
    public static BookingExtra Create(string code, string name, Money unitPrice, int quantity)
    {
        ArgumentNullException.ThrowIfNull(unitPrice);

        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(DomainErrorCodes.InvalidExtra, "An extra needs a code and a name.");
        }

        var trimmedCode = code.Trim();
        var trimmedName = name.Trim();
        if (trimmedCode.Length > MaxCodeLength || trimmedName.Length > MaxNameLength)
        {
            throw new DomainException(DomainErrorCodes.InvalidExtra, "The code or the name of the extra is too long.");
        }

        if (quantity is < 1 or > MaxQuantity)
        {
            throw new DomainException(DomainErrorCodes.InvalidExtra, $"The quantity of an extra must be between 1 and {MaxQuantity}.");
        }

        return new BookingExtra(trimmedCode, trimmedName, unitPrice, quantity);
    }
}
