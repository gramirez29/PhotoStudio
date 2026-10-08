namespace PhotoStudio.Infrastructure.Persistence;

/// <summary>
/// MongoDB connection settings, read from environment variables (Railway service variables in production).
/// </summary>
/// <param name="ConnectionString">MongoDB connection string (Atlas SRV string in production).</param>
/// <param name="DatabaseName">Name of the database.</param>
public sealed record MongoDbOptions(string ConnectionString, string DatabaseName)
{
    /// <summary>Environment variable that holds the connection string.</summary>
    public const string ConnectionStringVariable = "MONGODB_CONNECTION_STRING";

    /// <summary>Environment variable that holds the database name.</summary>
    public const string DatabaseNameVariable = "MONGODB_DATABASE_NAME";

    /// <summary>Database name used when <see cref="DatabaseNameVariable"/> is not set.</summary>
    public const string DefaultDatabaseName = "photostudio";

    /// <summary>
    /// Reads the options from the process environment.
    /// </summary>
    /// <returns>The options.</returns>
    /// <exception cref="InvalidOperationException">When the connection string variable is missing.</exception>
    public static MongoDbOptions FromEnvironment()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"The environment variable '{ConnectionStringVariable}' is not set.");
        }

        var databaseName = Environment.GetEnvironmentVariable(DatabaseNameVariable);
        return new MongoDbOptions(
            connectionString,
            string.IsNullOrWhiteSpace(databaseName) ? DefaultDatabaseName : databaseName);
    }

    /// <summary>
    /// Hides the connection string, which contains credentials, from logs and debugger output.
    /// </summary>
    /// <returns>A string without secrets.</returns>
    public override string ToString() => $"MongoDbOptions {{ DatabaseName = {DatabaseName} }}";
}
