namespace TUnit.Core;

/// <summary>
/// Timeout raised by generated closed-world execution. The runner uses
/// <see cref="ExecutionStillRunning"/> to avoid retrying while an abandoned
/// yielding operation can still own mutable resources.
/// </summary>
public sealed class GeneratedTimeoutException : TimeoutException
{
    public GeneratedTimeoutException(string message, Task executionCompletion, bool executionWasAbandoned)
        : base(message)
    {
        ExecutionCompletion = executionCompletion ?? throw new ArgumentNullException(nameof(executionCompletion));
        ExecutionWasAbandoned = executionWasAbandoned;
    }

    public Task ExecutionCompletion { get; }

    public bool ExecutionWasAbandoned { get; }

    public bool ExecutionStillRunning => !ExecutionCompletion.IsCompleted;
}
