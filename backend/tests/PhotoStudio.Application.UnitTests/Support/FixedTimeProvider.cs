namespace PhotoStudio.Application.UnitTests.Support;

/// <summary>
/// Clock that always returns the same instant, so handler results are deterministic.
/// </summary>
/// <param name="now">Instant returned by <see cref="GetUtcNow"/>.</param>
internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <summary>
    /// Returns the fixed instant.
    /// </summary>
    /// <returns>The instant given to the constructor.</returns>
    public override DateTimeOffset GetUtcNow() => now;
}
