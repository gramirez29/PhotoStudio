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
    private ClientContact(string name, string phone, string? email)
    {
        Name = name;
        Phone = phone;
        Email = email;
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
    /// Gets the client's email, normalized, or <see langword="null"/> when the photographer did not give one. The client
    /// portal sends its verification codes here, so without it the client cannot sign or submit proofs through the link.
    /// </summary>
    public string? Email { get; }

    /// <summary>
    /// Creates a contact after validating that the name and the phone are present and that the email, if given, has a valid shape.
    /// </summary>
    /// <param name="name">Client's full name.</param>
    /// <param name="phone">Client's phone number.</param>
    /// <param name="email">Client's email; blank is treated as not given.</param>
    /// <returns>The validated contact with trimmed values.</returns>
    /// <exception cref="DomainException">When the name or the phone is empty, or the email is not valid.</exception>
    public static ClientContact Create(string name, string phone, string? email = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The client name is required.");
        }

        if (string.IsNullOrWhiteSpace(phone))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The client phone is required.");
        }

        string? normalizedEmail = null;
        if (!string.IsNullOrWhiteSpace(email))
        {
            normalizedEmail = EmailAddress.TryNormalize(email)
                ?? throw new DomainException(DomainErrorCodes.InvalidEmail, "The client email is not valid.");
        }

        return new ClientContact(name.Trim(), phone.Trim(), normalizedEmail);
    }
}
