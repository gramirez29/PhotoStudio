using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Application.Identity.Studio;

/// <summary>
/// Hashes the codes of the web sign-in. The code service keys the hash with a secret and an identifier; the code is prefixed with
/// the purpose, so a code of the web sign-in can never match the hash of a code made for something else (the verification of the
/// email, or the client portal) even if the same user and digits came up.
/// </summary>
internal static class StudioCodes
{
    /// <summary>
    /// Hashes a code of the web sign-in.
    /// </summary>
    /// <param name="codeService">Generates and hashes codes.</param>
    /// <param name="userId">User the code is for.</param>
    /// <param name="code">The code, as generated or as typed.</param>
    /// <returns>The hash.</returns>
    public static string Hash(IOtpCodeService codeService, Guid userId, string code) => codeService.Hash(userId, "studio-login:" + code);
}
