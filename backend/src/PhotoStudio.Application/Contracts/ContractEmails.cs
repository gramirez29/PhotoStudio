using System.Net;
using PhotoStudio.Application.Abstractions;

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
        var text =
            $"Hola {clientName},\n\n" +
            "Gracias por firmar tu contrato. Esta es tu copia, tal como la firmaste. Guárdala para consultarla cuando quieras.\n\n" +
            "------------------------------\n" +
            $"{signedText}\n" +
            "------------------------------\n\n" +
            "PhotoSense Studio";

        var html =
            $"<p>Hola {WebUtility.HtmlEncode(clientName)},</p>" +
            "<p>Gracias por firmar tu contrato. Esta es tu copia, tal como la firmaste. Guárdala para consultarla cuando quieras.</p>" +
            $"<div style=\"white-space:pre-wrap;border:1px solid #cccccc;padding:12px;line-height:1.5\">{WebUtility.HtmlEncode(signedText)}</div>" +
            "<p>PhotoSense Studio</p>";

        return new EmailMessage(to, "Tu contrato firmado con PhotoSense Studio", text, html);
    }
}
