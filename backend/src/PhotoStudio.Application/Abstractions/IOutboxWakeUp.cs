namespace PhotoStudio.Application.Abstractions;

/// <summary>
/// Port that asks the process to deliver the outbox soon instead of waiting for the next scheduled run. It is only a hint:
/// the scheduled run still delivers everything, so ignoring a signal delays work but never loses it.
/// </summary>
public interface IOutboxWakeUp
{
    /// <summary>
    /// Asks for a maintenance pass as soon as possible. Many signals in a row are merged into one pass.
    /// </summary>
    void Signal();
}

/// <summary>
/// Wake-up used by processes that deliver on their own schedule (the worker): it does nothing.
/// </summary>
public sealed class NullOutboxWakeUp : IOutboxWakeUp
{
    /// <inheritdoc />
    public void Signal()
    {
        // Nothing to do: this process already delivers the outbox on its own schedule.
    }
}
