using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity;

/// <summary>
/// Whether an account has to prove it owns its email before using the app. It is a setting of the API (the
/// <c>AUTH_EMAIL_VERIFICATION_REQUIRED</c> variable, on by default) so an operator can turn it off if the email provider fails
/// and nobody can verify.
/// </summary>
/// <param name="Required">Whether unverified accounts are kept out of the app until they verify.</param>
public sealed record EmailVerificationSettings(bool Required)
{
    /// <summary>
    /// Tells whether a user may use the app: verification is not required, they proved their email, or they have no email at all
    /// (an account created before the email existed can never receive a code, so it is not locked out for something it cannot do).
    /// </summary>
    /// <param name="user">User to check.</param>
    /// <returns><see langword="true"/> when the user may use the app.</returns>
    public bool IsSatisfiedBy(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return !Required || user.IsEmailVerified || user.Email.Length == 0;
    }
}
