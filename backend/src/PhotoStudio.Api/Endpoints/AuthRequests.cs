using PhotoStudio.Application.Identity.Login;
using PhotoStudio.Application.Identity.Logout;
using PhotoStudio.Application.Identity.RefreshSession;
using PhotoStudio.Application.Identity.Register;

namespace PhotoStudio.Api.Endpoints;

/// <summary>
/// HTTP body to create an account.
/// </summary>
/// <param name="Username">Login name: 3 to 30 letters, digits, dots, hyphens or underscores.</param>
/// <param name="Email">Email address.</param>
/// <param name="Password">Password: 8 to 128 characters.</param>
/// <param name="Name">Display name.</param>
/// <param name="Phone">Phone number: 8 to 15 digits, with an optional leading plus sign.</param>
public sealed record RegisterRequest(string Username, string Email, string Password, string Name, string Phone);

/// <summary>
/// HTTP body to sign in.
/// </summary>
/// <param name="Username">Username of the user.</param>
/// <param name="Password">Password of the user.</param>
public sealed record LoginRequest(string Username, string Password);

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
    /// Maps the register request to its command.
    /// </summary>
    /// <param name="request">HTTP body.</param>
    /// <returns>The command.</returns>
    public static RegisterUserCommand ToCommand(this RegisterRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new RegisterUserCommand(request.Username, request.Email, request.Password, request.Name, request.Phone);
    }

    /// <summary>
    /// Maps the login request to its command.
    /// </summary>
    /// <param name="request">HTTP body.</param>
    /// <returns>The command.</returns>
    public static LoginCommand ToCommand(this LoginRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new LoginCommand(request.Username, request.Password);
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
