using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Common;
using PhotoStudio.Domain.Identity;

namespace PhotoStudio.Application.Identity.Register;

/// <summary>
/// Creates a user account and signs it in, so the person who just registered does not have to type the same data again.
/// The new user gets its own identifier, which is the tenant of everything it creates afterwards. Uniqueness of the
/// username and of the email is enforced by unique indexes, not by looking first, so two simultaneous registrations cannot
/// both win.
/// </summary>
/// <param name="users">User repository.</param>
/// <param name="refreshTokens">Refresh token repository.</param>
/// <param name="passwordHasher">Password hashing.</param>
/// <param name="sessions">Builds the session tokens.</param>
/// <param name="settings">Whether sign-up is open.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class RegisterUserHandler(
    IUserRepository users,
    IRefreshTokenRepository refreshTokens,
    IPasswordHasher passwordHasher,
    SessionIssuer sessions,
    RegistrationSettings settings,
    TimeProvider timeProvider) : ICommandHandler<RegisterUserCommand, AuthSessionResponse>
{
    /// <summary>
    /// Creates the user and starts its first session.
    /// </summary>
    /// <param name="command">Data of the new user.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The new session.</returns>
    /// <exception cref="RegistrationDisabledException">When sign-up is turned off.</exception>
    /// <exception cref="DomainException">When the username, email, name, phone or password is not valid.</exception>
    /// <exception cref="ConflictException">When the username or the email is already taken.</exception>
    public async Task<AuthSessionResponse> HandleAsync(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!settings.Enabled)
        {
            throw new RegistrationDisabledException();
        }

        // Validate everything before spending time hashing, so invalid input is cheap to refuse.
        User.ValidatePassword(command.Password);
        var username = User.NormalizeUsername(command.Username);
        var email = User.NormalizeEmail(command.Email);
        var name = User.NormalizeName(command.Name);
        var phone = User.NormalizePhone(command.Phone);

        var now = timeProvider.GetUtcNow();
        var user = User.Create(Guid.CreateVersion7(), username, email, passwordHasher.Hash(command.Password), name, phone, now);

        await users.AddAsync(user, cancellationToken);

        var session = sessions.Issue(user, Guid.CreateVersion7(), now);
        await refreshTokens.AddAsync(session.RefreshToken, cancellationToken);
        return session.Response;
    }
}
