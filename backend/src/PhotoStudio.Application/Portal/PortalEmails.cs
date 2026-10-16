using System.Net;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.Portal;

/// <summary>
/// Builds the emails the portal sends. The texts are in Spanish, the language of the clients.
/// </summary>
internal static class PortalEmails
{
    /// <summary>
    /// Builds the email that carries a verification code.
    /// </summary>
    /// <param name="to">Recipient address.</param>
    /// <param name="clientName">Name of the client, for the greeting.</param>
    /// <param name="code">Verification code in clear text.</param>
    /// <returns>The email.</returns>
    public static EmailMessage VerificationCode(string to, string clientName, string code)
    {
        var minutes = (int)PortalOtp.Lifetime.TotalMinutes;
        var text =
            $"Hola {clientName},\n\n" +
            $"Tu código de verificación es: {code}\n\n" +
            $"Vence en {minutes} minutos. Si no lo pediste, ignora este mensaje: nadie puede usarlo sin tu enlace.\n\n" +
            "PhotoSense Studio";

        var html =
            $"<p>Hola {WebUtility.HtmlEncode(clientName)},</p>" +
            $"<p>Tu código de verificación es:</p><p style=\"font-size:28px;font-weight:700;letter-spacing:6px\">{code}</p>" +
            $"<p>Vence en {minutes} minutos. Si no lo pediste, ignora este mensaje: nadie puede usarlo sin tu enlace.</p>" +
            "<p>PhotoSense Studio</p>";

        return new EmailMessage(to, "Tu código de verificación de PhotoSense Studio", text, html);
    }
}
