using PhotoStudio.Application.Abstractions;

namespace PhotoStudio.Application.Identity.Logout;

/// <summary>
/// Closes a session by revoking its whole token family. It is idempotent and never reveals whether the token existed: the
/// API answers the same way either way.
/// </summary>
/// <param name="refreshTokens">Refresh token repository.</param>
/// <param name="tokenGenerator">Hashes the presented secret.</param>
/// <param name="timeProvider">Clock abstraction.</param>
public sealed class LogoutHandler(
    IRefreshTokenRepository refreshTokens,
    IRefreshTokenGenerator tokenGenerator,
    TimeProvider timeProvider) : ICommandHandler<LogoutCommand, LogoutResponse>
{
    /// <summary>
    /// Revokes the session that owns the token.
    /// </summary>
    /// <param name="command">Refresh token secret.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>Whether a session was closed.</returns>
    public async Task<LogoutResponse> HandleAsync(LogoutCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return new LogoutResponse(false);
        }

        var token = await refreshTokens.GetByHashAsync(tokenGenerator.Hash(command.RefreshToken), cancellationToken);
        if (token is null)
        {
            return new LogoutResponse(false);
        }

        await refreshTokens.RevokeFamilyAsync(token.FamilyId, timeProvider.GetUtcNow(), cancellationToken);
        return new LogoutResponse(true);
    }
}
