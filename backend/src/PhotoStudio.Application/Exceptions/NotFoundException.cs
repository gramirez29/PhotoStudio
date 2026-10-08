namespace PhotoStudio.Application.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist.
/// </summary>
/// <param name="resource">Name of the resource type, for example "Booking".</param>
/// <param name="id">Identifier that was requested.</param>
public sealed class NotFoundException(string resource, Guid id) : Exception($"{resource} '{id}' was not found.")
{
    /// <summary>
    /// Gets the name of the resource type.
    /// </summary>
    public string Resource { get; } = resource;

    /// <summary>
    /// Gets the identifier that was requested.
    /// </summary>
    public Guid ResourceId { get; } = id;
}
