using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Emails;
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
        var content = new EmailContent(
            $"Tu código para verificar tu correo es {code}.",
            "Verifica tu correo",
            [$"Hola {name}, este es el código para verificar tu correo en PhotoSense Studio:"],
            Note: $"Vence en {minutes} minutos. Si no creaste una cuenta, ignora este mensaje: nadie puede usar tu correo sin este código.",
            Code: code);

        return BrandedEmail.Message(to, "Verifica tu correo de PhotoSense Studio", content);
    }

    /// <summary>
    /// Builds the email that carries the code to choose a new password.
    /// </summary>
    /// <param name="to">Recipient address, the verified email of the account.</param>
    /// <param name="name">Name of the user, for the greeting.</param>
    /// <param name="code">Code in clear text.</param>
    /// <returns>The email.</returns>
    public static EmailMessage PasswordResetCode(string to, string name, string code)
    {
        var minutes = (int)Domain.Identity.PasswordResetCode.Lifetime.TotalMinutes;
        var content = new EmailContent(
            $"Tu código para cambiar tu contraseña es {code}.",
            "Cambia tu contraseña",
            [$"Hola {name}, este es el código para elegir una contraseña nueva en PhotoSense Studio:"],
            Note: $"Vence en {minutes} minutos y sirve una sola vez. Si no lo pediste, ignora este mensaje: tu contraseña no cambia sin este código, y no se lo des a nadie.",
            Code: code);

        return BrandedEmail.Message(to, "Código para cambiar tu contraseña de PhotoSense Studio", content);
    }

    /// <summary>
    /// Builds the email that tells the owner that the password of the account changed.
    /// </summary>
    /// <param name="to">Recipient address, the verified email of the account.</param>
    /// <param name="name">Name of the user, for the greeting.</param>
    /// <returns>The email.</returns>
    public static EmailMessage PasswordChanged(string to, string name)
    {
        var content = new EmailContent(
            "Tu contraseña cambió.",
            "Tu contraseña cambió",
            [$"Hola {name}, la contraseña de tu cuenta de PhotoSense Studio se acaba de cambiar y se cerraron todas tus sesiones."],
            Note: "Si fuiste tú, no tienes que hacer nada. Si no fuiste tú, alguien con acceso a tu correo pidió el cambio: cambia también la contraseña de tu correo y vuelve a recuperar tu cuenta cuanto antes.");

        return BrandedEmail.Message(to, "Tu contraseña de PhotoSense Studio cambió", content);
    }

    /// <summary>
    /// Builds the email that carries the code of the second factor of the web sign-in.
    /// </summary>
    /// <param name="to">Recipient address, the verified email of the account.</param>
    /// <param name="name">Name of the user, for the greeting.</param>
    /// <param name="code">Code in clear text.</param>
    /// <returns>The email.</returns>
    public static EmailMessage StudioLoginCode(string to, string name, string code)
    {
        var minutes = (int)StudioLoginChallenge.Lifetime.TotalMinutes;
        var content = new EmailContent(
            $"Tu código para entrar es {code}.",
            "Tu código para entrar",
            [$"Hola {name}, este es el código para entrar a la web de PhotoSense Studio:"],
            Note: $"Vence en {minutes} minutos y sirve una sola vez. Si no intentaste entrar, no compartas este código con nadie y cambia tu contraseña desde la app: alguien más ya la conoce.",
            Code: code);

        return BrandedEmail.Message(to, "Tu código para entrar a PhotoSense Studio", content);
    }
}
