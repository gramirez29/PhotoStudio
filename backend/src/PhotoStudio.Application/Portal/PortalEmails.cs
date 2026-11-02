using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Emails;
using PhotoStudio.Domain.Portal;

namespace PhotoStudio.Application.Portal;

/// <summary>
/// Builds the emails of the client portal, in Spanish and in the look of the studio.
/// </summary>
internal static class PortalEmails
{
    /// <summary>
    /// Builds the email with the code that proves the client owns the address the photographer registered.
    /// </summary>
    /// <param name="to">Address of the client.</param>
    /// <param name="clientName">Name of the client.</param>
    /// <param name="code">The six digits.</param>
    /// <returns>The email.</returns>
    public static EmailMessage VerificationCode(string to, string clientName, string code)
    {
        var minutes = (int)PortalOtp.Lifetime.TotalMinutes;
        var content = new EmailContent(
            $"Tu código de verificación es {code}.",
            "Tu código de verificación",
            [$"Hola {clientName}, este es tu código de verificación:"],
            Note: $"Vence en {minutes} minutos. Si no lo pediste, ignora este mensaje: nadie puede usarlo sin tu enlace.",
            Code: code);

        return BrandedEmail.Message(to, "Tu código de verificación de PhotoSense Studio", content);
    }
}
