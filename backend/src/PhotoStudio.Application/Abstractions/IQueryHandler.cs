namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Handles a read-only query.
/// </summary>
/// <typeparam name="TQuery">Type of the query.</typeparam>
/// <typeparam name="TResult">Type of the result.</typeparam>
public interface IQueryHandler<in TQuery, TResult>
{
    /// <summary>
    /// Executes the query.
    /// </summary>
    /// <param name="query">Query to execute.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The query result.</returns>
    Task<TResult> HandleAsync(TQuery query, CancellationToken cancellationToken);
}
