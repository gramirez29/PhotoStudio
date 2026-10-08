using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using PhotoStudio.Application.Abstractions;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Application.Identity.EnsureAccount;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Infrastructure.Identity;

/// <summary>
/// Creates the photographer account at startup from environment variables, since the app has no public sign-up. The account
/// keeps the identifier given in <see cref="PhotographerIdVariable"/>, so the bookings that already exist under that
/// tenant stay reachable. If MongoDB is not ready yet it retries a few times; it never stops the API from starting.
/// </summary>
/// <param name="scopeFactory">Creates the scope in which the use case runs.</param>
/// <param name="timeProvider">Clock abstraction used for the waits between attempts.</param>
/// <param name="logger">Logger.</param>
public sealed partial class PhotographerAccountSeeder(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<PhotographerAccountSeeder> logger) : BackgroundService
{
    /// <summary>Environment variable that holds the login email of the account.</summary>
    public const string EmailVariable = "SEED_PHOTOGRAPHER_EMAIL";

    /// <summary>Environment variable that holds the password of the account.</summary>
    public const string PasswordVariable = "SEED_PHOTOGRAPHER_PASSWORD";

    /// <summary>Environment variable that holds the photographer (tenant) identifier of the account; optional.</summary>
    public const string PhotographerIdVariable = "SEED_PHOTOGRAPHER_ID";

    private const int MaxAttempts = 5;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Ensures the account exists, retrying while MongoDB is unreachable.
    /// </summary>
    /// <param name="stoppingToken">Signaled when the host is shutting down.</param>
    /// <returns>A task that completes when the account is ensured, the attempts run out or the configuration is invalid.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var email = Environment.GetEnvironmentVariable(EmailVariable);
        var password = Environment.GetEnvironmentVariable(PasswordVariable);
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            LogNotConfigured(logger, EmailVariable, PasswordVariable);
            return;
        }

        if (!TryReadPhotographerId(out var photographerId))
        {
            LogInvalidPhotographerId(logger, PhotographerIdVariable);
            return;
        }

        var command = new EnsurePhotographerAccountCommand(photographerId, email, password);
        for (var attempt = 1; attempt <= MaxAttempts && !stoppingToken.IsCancellationRequested; attempt++)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<EnsurePhotographerAccountCommand, EnsureAccountResult>>();
                var result = await handler.HandleAsync(command, stoppingToken);
                ReportResult(result, photographerId);
                return;
            }
            catch (DomainException exception)
            {
                // The configured email or password is not acceptable; retrying cannot fix it.
                LogInvalidConfiguration(logger, exception.Message);
                return;
            }
            catch (Exception exception) when (exception is MongoException or TimeoutException or ConflictException)
            {
                LogAttemptFailed(logger, attempt, MaxAttempts, exception);
                await Task.Delay(RetryDelay, timeProvider, stoppingToken).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    /// <summary>
    /// Reads the optional photographer identifier.
    /// </summary>
    /// <param name="photographerId">The identifier, or <see langword="null"/> when the variable is not set.</param>
    /// <returns><see langword="false"/> when the variable is set but is not a valid identifier.</returns>
    private static bool TryReadPhotographerId(out Guid? photographerId)
    {
        var value = Environment.GetEnvironmentVariable(PhotographerIdVariable);
        photographerId = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
        {
            return false;
        }

        photographerId = parsed;
        return true;
    }

    /// <summary>
    /// Logs what the use case did, and warns when the existing account has another identifier than the configured one.
    /// </summary>
    /// <param name="result">Outcome of the use case.</param>
    /// <param name="configuredId">Identifier from the environment, if any.</param>
    private void ReportResult(EnsureAccountResult result, Guid? configuredId)
    {
        LogAccountEnsured(logger, result.Outcome, result.PhotographerId);
        if (configuredId is not null && configuredId != result.PhotographerId)
        {
            LogIdentifierMismatch(logger, PhotographerIdVariable, result.PhotographerId);
        }
    }

    /// <summary>
    /// Logs that no account is configured.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="emailVariable">Name of the email variable.</param>
    /// <param name="passwordVariable">Name of the password variable.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "No photographer account is configured ({EmailVariable} and {PasswordVariable} are not set); nobody can sign in unless an account already exists.")]
    private static partial void LogNotConfigured(ILogger logger, string emailVariable, string passwordVariable);

    /// <summary>
    /// Logs a photographer identifier that is not a valid GUID.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="variable">Name of the variable.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "{Variable} is not a valid GUID; the photographer account was not created.")]
    private static partial void LogInvalidPhotographerId(ILogger logger, string variable);

    /// <summary>
    /// Logs an email or password that the use case rejected.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="reason">Why it was rejected.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "The configured photographer account is not valid and was not created: {Reason}")]
    private static partial void LogInvalidConfiguration(ILogger logger, string reason);

    /// <summary>
    /// Logs a failed attempt that will be retried.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="attempt">Attempt number.</param>
    /// <param name="maxAttempts">Attempts allowed.</param>
    /// <param name="exception">Failure cause.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The photographer account could not be ensured (attempt {Attempt} of {MaxAttempts}).")]
    private static partial void LogAttemptFailed(ILogger logger, int attempt, int maxAttempts, Exception exception);

    /// <summary>
    /// Logs the outcome.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="outcome">What the use case did.</param>
    /// <param name="photographerId">Identifier of the account.</param>
    [LoggerMessage(Level = LogLevel.Information, Message = "Photographer account {Outcome} (id {PhotographerId}).")]
    private static partial void LogAccountEnsured(ILogger logger, EnsureAccountOutcome outcome, Guid photographerId);

    /// <summary>
    /// Logs that the existing account has a different identifier than the configured one.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="variable">Name of the variable.</param>
    /// <param name="existingId">Identifier of the existing account.</param>
    [LoggerMessage(Level = LogLevel.Warning, Message = "The account already existed with id {ExistingId}, which differs from {Variable}; the existing id is kept, so bookings stored under the configured id are not reachable.")]
    private static partial void LogIdentifierMismatch(ILogger logger, string variable, Guid existingId);
}
