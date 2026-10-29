using System.Net;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity;

/// <summary>
/// Builds the emails of the accounts of photographers. The texts are in Spanish, the language of the product.
/// </summary>
internal static class IdentityEmails
{
    /// <summary>
    /// Builds the email that carries the code to verify the address of an account.
    /// </summary>
    /// <param name="to">Recipient address, the one being verified.</param>
    /// <param name="name">Name of the user, for the greeting.</param>
    /// <param name="code">Verification code in clear text.</param>
    /// <returns>The email.</returns>
    public static EmailMessage VerificationCode(string to, string name, string code)
    {
        var minutes = (int)EmailVerification.Lifetime.TotalMinutes;
        var text =
            $"Hola {name},\n\n" +
            $"Tu código para verificar tu correo en PhotoSense Studio es: {code}\n\n" +
            $"Vence en {minutes} minutos. Si no creaste una cuenta, ignora este mensaje: nadie puede usar tu correo sin este código.\n\n" +
            "PhotoSense Studio";

        var html =
            $"<p>Hola {WebUtility.HtmlEncode(name)},</p>" +
            $"<p>Tu código para verificar tu correo en PhotoSense Studio es:</p><p style=\"font-size:28px;font-weight:700;letter-spacing:6px\">{code}</p>" +
            $"<p>Vence en {minutes} minutos. Si no creaste una cuenta, ignora este mensaje: nadie puede usar tu correo sin este código.</p>" +
            "<p>PhotoSense Studio</p>";

        return new EmailMessage(to, "Verifica tu correo de PhotoSense Studio", text, html);
    }
}
