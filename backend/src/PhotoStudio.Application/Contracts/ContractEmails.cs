using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Emails;

namespace PhotoStudio.Application.Contracts;

/// <summary>
/// Builds the emails about contracts. The texts are in Spanish, the language of the clients.
/// </summary>
internal static class ContractEmails
{
    /// <summary>
    /// Builds the email that carries the client's copy of the contract they signed.
    /// </summary>
    /// <param name="to">Recipient address.</param>
    /// <param name="clientName">Name of the client, for the greeting.</param>
    /// <param name="signedText">Exact text of the contract the client signed.</param>
    /// <returns>The email.</returns>
    public static EmailMessage SignedCopy(string to, string clientName, string signedText)
    {
        var content = new EmailContent(
            "Esta es tu copia del contrato que firmaste.",
            "Tu contrato firmado",
            [$"Hola {clientName}, gracias por firmar tu contrato. Esta es tu copia, tal como la firmaste. Guárdala para consultarla cuando quieras."],
            Document: signedText);

        return BrandedEmail.Message(to, "Tu contrato firmado con PhotoSense Studio", content);
    }
}
