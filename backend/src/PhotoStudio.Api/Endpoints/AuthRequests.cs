using PhotoStudio.Application.Identity.Login;
using PhotoStudio.Application.Identity.Logout;
using PhotoStudio.Application.Identity.RefreshSession;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// HTTP body to sign in.
/// </summary>
/// <param name="Email">Email of the photographer.</param>
/// <param name="Password">Password of the photographer.</param>
public sealed record LoginRequest(string Email, string Password);

/// <summary>
/// HTTP body to exchange a refresh token for a new session.
/// </summary>
/// <param name="RefreshToken">Refresh token secret held by the app.</param>
public sealed record RefreshRequest(string RefreshToken);

/// <summary>
/// HTTP body to sign out.
/// </summary>
/// <param name="RefreshToken">Refresh token secret of the session to close.</param>
public sealed record LogoutRequest(string RefreshToken);

/// <summary>
/// Maps the authentication request DTOs to application commands.
/// </summary>
public static class AuthRequestMappings
{
    /// <summary>
    /// Maps the login request to its command.
    /// </summary>
    /// <param name="request">HTTP body.</param>
    /// <returns>The command.</returns>
    public static LoginCommand ToCommand(this LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new LoginCommand(request.Email, request.Password);
    }

    /// <summary>
    /// Maps the refresh request to its command.
    /// </summary>
    /// <param name="request">HTTP body.</param>
    /// <returns>The command.</returns>
    public static RefreshSessionCommand ToCommand(this RefreshRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new RefreshSessionCommand(request.RefreshToken);
    }

    /// <summary>
    /// Maps the logout request to its command.
    /// </summary>
    /// <param name="request">HTTP body.</param>
    /// <returns>The command.</returns>
    public static LogoutCommand ToCommand(this LogoutRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new LogoutCommand(request.RefreshToken);
    }
}
