namespace PhotoStudio.Infrastructure.IntegrationTests.Storage;

/// <summary>
/// Groups the tests that change process environment variables so they never run at the same time as each other.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EnvironmentCollection
{
    /// <summary>
    /// Name of the collection.
    /// </summary>
    public const string Name = "environment-variables";
}
