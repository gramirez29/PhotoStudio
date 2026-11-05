using PhotoStudio.Application.Abstractions;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.RecoverPassword;

/// <summary>
/// Finds the account a person who forgot their password is talking about, from a text that can be the username or the email. Only an
/// account whose email was verified can recover its password by email: an address nobody proved is not a way to prove who someone is.
/// </summary>
/// <param name="users">User repository.</param>
internal sealed class AccountFinder(IUserRepository users)
{
    /// <summary>
    /// Looks the account up.
    /// </summary>
    /// <param name="identifier">Username or email as typed; an at sign makes it an email.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The user, or <see langword="null"/> when the text matches no account or the account cannot recover its password by email.</returns>
    public async Task<User?> FindRecoverableAsync(string? identifier, CancellationToken cancellationToken)
    {
        var typed = identifier?.Trim();
        if (string.IsNullOrEmpty(typed))
        {
            return null;
        }

        User? user;
        if (typed.Contains('@', StringComparison.Ordinal))
        {
            var email = User.TryNormalizeEmail(typed);
            user = email is null ? null : await users.GetByEmailAsync(email, cancellationToken);
        }
        else
        {
            var username = User.TryNormalizeUsername(typed);
            user = username is null ? null : await users.GetByUsernameAsync(username, cancellationToken);
        }

        return user is { IsEmailVerified: true } && user.Email.Length > 0 ? user : null;
    }
}
