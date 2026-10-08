using System.Globalization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PhotoStudio.Application.Exceptions;
using PhotoStudio.Domain.Common;

namespace PhotoStudio.Api.Errors;

/// <summary>
/// Translates exceptions into RFC 9457 problem details with a stable <c>code</c> extension that clients can branch on.
/// </summary>
/// <param name="problemDetailsService">Service that writes problem details responses.</param>
/// <param name="logger">Logger.</param>
public sealed partial class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <summary>
    /// Maps the exception to a status code and writes the problem details response.
    /// </summary>
    /// <param name="httpContext">Current HTTP context.</param>
    /// <param name="exception">Unhandled exception.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><see langword="true"/> when the response was written.</returns>
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(exception);

        var (status, title, code) = exception switch
        {
            DomainException domain when IsStateConflict(domain.Code) =>
                (StatusCodes.Status409Conflict, "The booking does not allow this action right now.", domain.Code),
            DomainException domain =>
                (StatusCodes.Status422UnprocessableEntity, "A business rule was violated.", domain.Code),
            ConflictException conflict =>
                (StatusCodes.Status409Conflict, "The request conflicts with the current state.", conflict.Code),
            AuthenticationFailedException authentication =>
                (StatusCodes.Status401Unauthorized, "Authentication failed.", authentication.Code),
            AccountLockedException locked =>
                (StatusCodes.Status429TooManyRequests, "Too many failed attempts.", locked.Code),
            NotFoundException =>
                (StatusCodes.Status404NotFound, "The resource was not found.", ApplicationErrorCodes.NotFound),
            BadHttpRequestException badRequest =>
                (badRequest.StatusCode, "The request is not valid.", "request.invalid"),
            ArgumentException =>
                (StatusCodes.Status400BadRequest, "The request is not valid.", "request.invalid"),
            _ =>
                (StatusCodes.Status500InternalServerError, "An unexpected error occurred.", "server.error"),
        };

        if (status == StatusCodes.Status500InternalServerError)
        {
            LogUnhandledException(logger, httpContext.Request.Path, exception);
        }

        httpContext.Response.StatusCode = status;
        if (status == StatusCodes.Status401Unauthorized)
        {
            httpContext.Response.Headers.WWWAuthenticate = "Bearer";
        }

        if (exception is AccountLockedException accountLocked)
        {
            httpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(accountLocked.RetryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
            Instance = httpContext.Request.Path,
        };
        problem.Extensions["code"] = code;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    /// <summary>
    /// Indicates whether a domain error means "not allowed in the current state" (409) rather than invalid input (422).
    /// </summary>
    /// <param name="code">Domain error code.</param>
    /// <returns><see langword="true"/> for state conflicts.</returns>
    private static bool IsStateConflict(string code) => code is
        DomainErrorCodes.InvalidTransition or
        DomainErrorCodes.GuardFailed or
        DomainErrorCodes.ContractAlreadySigned or
        DomainErrorCodes.InvalidPaymentStatus;

    /// <summary>
    /// Logs an exception that was not mapped to a known error.
    /// </summary>
    /// <param name="logger">Logger.</param>
    /// <param name="path">Request path.</param>
    /// <param name="exception">Unhandled exception.</param>
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception while processing {Path}")]
    private static partial void LogUnhandledException(ILogger logger, PathString path, Exception exception);
}
