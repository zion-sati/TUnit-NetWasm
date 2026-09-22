namespace TUnit.Core;

/// <summary>
/// Cancellation raised by generated closed-world execution when the cancelled
/// operation has not returned yet. The completion task covers the remaining
/// test cleanup so runners do not release outer resources too early.
/// </summary>
public sealed class GeneratedCancellationException : OperationCanceledException
{
    public GeneratedCancellationException(
        CancellationToken cancellationToken,
        Task executionCompletion,
        bool executionWasAbandoned)
        : base("The test run was cancelled.", cancellationToken)
    {
        ExecutionCompletion = executionCompletion ?? throw new ArgumentNullException(nameof(executionCompletion));
        ExecutionWasAbandoned = executionWasAbandoned;
    }

    public Task ExecutionCompletion { get; }

    public bool ExecutionWasAbandoned { get; }

    public bool ExecutionStillRunning => !ExecutionCompletion.IsCompleted;
}
