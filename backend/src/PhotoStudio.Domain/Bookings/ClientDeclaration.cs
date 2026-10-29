using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Bookings;

/// <summary>
/// What the client declares when signing the contract through the portal: who they are, how many people come, where the
/// session takes place and whether the photos may be published. These are the blanks of the contract that only the client knows.
/// </summary>
public sealed record ClientDeclaration
{
    /// <summary>Largest number of people a session can declare.</summary>
    public const int MaxParticipants = 100;

    /// <summary>Longest session location accepted, in characters.</summary>
    public const int MaxLocationLength = 200;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientDeclaration"/> class. Use <see cref="Create"/>, which validates the values.
    /// </summary>
    private ClientDeclaration(string idNumber, int participantCount, string? sessionLocation, ImageUse imageUse)
    {
        IdNumber = idNumber;
        ParticipantCount = participantCount;
        SessionLocation = sessionLocation;
        ImageUse = imageUse;
    }

    /// <summary>Gets the identity number of the signer, digits only (cédula of 9 digits or DIMEX of 11 or 12).</summary>
    public string IdNumber { get; }

    /// <summary>Gets the number of people who take part in the session.</summary>
    public int ParticipantCount { get; }

    /// <summary>Gets the place of the session as the client wrote it, or <see langword="null"/> when they left it blank.</summary>
    public string? SessionLocation { get; }

    /// <summary>Gets what the client allowed to do with the photos.</summary>
    public ImageUse ImageUse { get; }

    /// <summary>
    /// Creates a declaration after validating and normalizing its values.
    /// </summary>
    /// <param name="idNumber">Identity number as typed; spaces and hyphens are ignored, so <c>1-1234-5678</c> is accepted.</param>
    /// <param name="participantCount">Number of people in the session, between 1 and <see cref="MaxParticipants"/>.</param>
    /// <param name="sessionLocation">Place of the session; optional.</param>
    /// <param name="imageUse">What the client allows to do with the photos.</param>
    /// <returns>The declaration.</returns>
    /// <exception cref="DomainException">When the identity number, the number of people or the location is not valid.</exception>
    public static ClientDeclaration Create(string idNumber, int participantCount, string? sessionLocation, ImageUse imageUse)
    {
        var digits = new string((idNumber ?? string.Empty).Where(character => character is not ('-' or ' ')).ToArray());
        if (digits.Length is not (9 or 11 or 12) || !digits.All(char.IsAsciiDigit))
        {
            throw new DomainException(
                DomainErrorCodes.InvalidIdNumber,
                "The identity number must be a cédula of 9 digits or a DIMEX of 11 or 12 digits.");
        }

        if (participantCount is < 1 or > MaxParticipants)
        {
            throw new DomainException(
                DomainErrorCodes.InvalidContractDetails,
                $"The number of people must be between 1 and {MaxParticipants}.");
        }

        var location = string.IsNullOrWhiteSpace(sessionLocation) ? null : sessionLocation.Trim();
        if (location is { Length: > MaxLocationLength })
        {
            throw new DomainException(
                DomainErrorCodes.InvalidContractDetails,
                $"The location cannot be longer than {MaxLocationLength} characters.");
        }

        if (!Enum.IsDefined(imageUse))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The image use choice is required.");
        }

        return new ClientDeclaration(digits, participantCount, location, imageUse);
    }
}
