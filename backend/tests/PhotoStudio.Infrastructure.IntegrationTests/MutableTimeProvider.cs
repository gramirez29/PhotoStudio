namespace PhotoStudio.Infrastructure.IntegrationTests;

/// <summary>
/// Clock whose current instant a test sets and advances, so retry delays and leases can be checked without waiting.
/// </summary>
/// <param name="now">Initial instant.</param>
internal sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    /// <summary>
    /// Returns the current instant of the clock.
    /// </summary>
    /// <returns>The instant last set or reached by <see cref="Advance"/>.</returns>
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>
    /// Moves the clock forward.
    /// </summary>
    /// <param name="amount">Time to add.</param>
    public void Advance(TimeSpan amount) => _now += amount;
}
