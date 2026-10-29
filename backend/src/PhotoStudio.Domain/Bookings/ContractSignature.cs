using PhotoStudio.Domain.Common;

namespace PhotoStudio.Domain.Bookings;

/// <summary>
/// Evidence that the booking contract was signed: who, through which channel, when and which template version. A signature made
/// by the client through the portal also keeps what the client declared and the exact text they were shown.
/// </summary>
public sealed record ContractSignature
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContractSignature"/> class. Use the static factory methods, which validate the values.
    /// </summary>
    private ContractSignature(
        string signerName,
        string templateVersion,
        Actor signedBy,
        Channel channel,
        DateTimeOffset signedAt,
        ClientDeclaration? declaration,
        string? signedText)
    {
        SignerName = signerName;
        TemplateVersion = templateVersion;
        SignedBy = signedBy;
        Channel = channel;
        SignedAt = signedAt;
        Declaration = declaration;
        SignedText = signedText;
    }

    /// <summary>Gets the name the signer typed when signing.</summary>
    public string SignerName { get; }

    /// <summary>Gets the version of the contract template that was signed.</summary>
    public string TemplateVersion { get; }

    /// <summary>Gets who registered the signature.</summary>
    public Actor SignedBy { get; }

    /// <summary>Gets the channel through which the contract was signed.</summary>
    public Channel Channel { get; }

    /// <summary>Gets the instant the signature was registered (UTC).</summary>
    public DateTimeOffset SignedAt { get; }

    /// <summary>Gets what the client declared when signing through the portal; <see langword="null"/> for a signature registered by the photographer.</summary>
    public ClientDeclaration? Declaration { get; }

    /// <summary>Gets the exact text of the contract the client signed through the portal; <see langword="null"/> for a signature registered by the photographer.</summary>
    public string? SignedText { get; }

    /// <summary>
    /// Creates a signature record after validating its values.
    /// </summary>
    /// <param name="signerName">Name typed by the signer.</param>
    /// <param name="templateVersion">Contract template version.</param>
    /// <param name="signedBy">Actor that registered the signature.</param>
    /// <param name="channel">Channel of the signature.</param>
    /// <param name="signedAt">Instant of the signature.</param>
    /// <param name="declaration">What the client declared, when they signed through the portal.</param>
    /// <param name="signedText">Exact text the client signed, when they signed through the portal.</param>
    /// <returns>The signature record.</returns>
    /// <exception cref="DomainException">When the signer name or template version is empty.</exception>
    public static ContractSignature Create(
        string signerName,
        string templateVersion,
        Actor signedBy,
        Channel channel,
        DateTimeOffset signedAt,
        ClientDeclaration? declaration = null,
        string? signedText = null)
    {
        if (string.IsNullOrWhiteSpace(signerName))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The signer name is required.");
        }

        if (string.IsNullOrWhiteSpace(templateVersion))
        {
            throw new DomainException(DomainErrorCodes.RequiredValue, "The contract template version is required.");
        }

        return new ContractSignature(signerName.Trim(), templateVersion.Trim(), signedBy, channel, signedAt, declaration, signedText);
    }
}
