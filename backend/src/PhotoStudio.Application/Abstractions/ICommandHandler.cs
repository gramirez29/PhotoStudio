namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Handles a command that changes state and returns a result.
/// </summary>
/// <typeparam name="TCommand">Type of the command.</typeparam>
/// <typeparam name="TResult">Type of the result.</typeparam>
public interface ICommandHandler<in TCommand, TResult>
{
    /// <summary>
    /// Executes the command.
    /// </summary>
    /// <param name="command">Command to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The result of the command.</returns>
    Task<TResult> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
