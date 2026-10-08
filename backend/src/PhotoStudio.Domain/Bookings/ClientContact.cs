using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Bookings;

/// <summary>
/// Contact data of the photographer's client for a booking.
/// </summary>
public sealed record ClientContact
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ClientContact"/> class. Use the static factory methods, which validate the values.
    /// </summary>
    private ClientContact(string name, string phone)
    {
        Name = name;
        Phone = phone;
    }

    /// <summary>
    /// Gets the client's full name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the client's phone number, used for WhatsApp links and verification codes.
    /// </summary>
    public string Phone { get; }

    /// <summary>
    /// Creates a contact after validating that both values are present.
    /// </summary>
    /// <param name="name">Client's full name.</param>
    /// <param name="phone">Client's phone number.</param>
    /// <returns>The validated contact with trimmed values.</returns>
    /// <exception cref="DomainException">When the name or the phone is empty.</exception>
    public static ClientContact Create(string name, string phone)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The client name is required.");
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The client phone is required.");
        }

        return new ClientContact(name.Trim(), phone.Trim());
    }
}
