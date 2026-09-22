using System.Collections.ObjectModel;

namespace TUnit.Core;

/// <summary>
/// A lifecycle action emitted alongside a generated test entry.
/// </summary>
public sealed class GeneratedLifecycleAction
{
    public GeneratedLifecycleAction(
        GeneratedLifecycleStage stage,
        int order,
        Func<object?, CancellationToken, ValueTask> invoke,
        TimeSpan? timeout = null)
    {
        Invoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
        Stage = stage;
        Order = order;
        Timeout = timeout;
    }

    public GeneratedLifecycleStage Stage { get; }

    public int Order { get; }

    public Func<object?, CancellationToken, ValueTask> Invoke { get; }

    public TimeSpan? Timeout { get; }

    public async ValueTask InvokeAsync(object? instance, CancellationToken cancellationToken)
    {
        if (Timeout is not TimeSpan timeout)
        {
            await Invoke(instance, cancellationToken);
            return;
        }

        var executionSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task execution;
        try
        {
            execution = Invoke(instance, executionSource.Token).AsTask();
        }
        catch
        {
            executionSource.Dispose();
            throw;
        }

        var deadline = Task.Delay(timeout, cancellationToken);
        var completed = await Task.WhenAny(execution, deadline);
        if (ReferenceEquals(completed, execution))
        {
            executionSource.Dispose();
            await execution;
            return;
        }

        CancelWithoutThrowing(executionSource);
        var executionStillRunning = !execution.IsCompleted;
        if (executionStillRunning)
        {
            _ = ObserveAndDisposeAsync(execution, executionSource);
        }
        else
        {
            executionSource.Dispose();
        }

        if (cancellationToken.IsCancellationRequested)
        {
            throw new GeneratedCancellationException(
                cancellationToken,
                execution,
                executionStillRunning);
        }

        throw new GeneratedTimeoutException(
            $"Lifecycle hook exceeded its timeout of {timeout.TotalMilliseconds.ToString(global::System.Globalization.CultureInfo.InvariantCulture)} ms.",
            execution,
            executionStillRunning);
    }

    private static async Task ObserveAndDisposeAsync(Task execution, CancellationTokenSource source)
    {
        try
        {
            await execution;
        }
        catch
        {
            // The timeout is the reported failure. Observe any late exception so
            // an abandoned yielding hook cannot raise an unobserved-task failure.
        }
        finally
        {
            source.Dispose();
        }
    }

    private static void CancelWithoutThrowing(CancellationTokenSource source)
    {
        try
        {
            source.Cancel();
        }
        catch
        {
            // Timeout/cancellation remains the primary outcome. A callback failure
            // must not lose the completion boundary for the still-running hook.
        }
    }
}

/// <summary>
/// The ordered setup and teardown actions for a generated test case.
/// </summary>
public sealed class GeneratedLifecycle
{
    public static GeneratedLifecycle Empty { get; } = new([]);

    public GeneratedLifecycle(IEnumerable<GeneratedLifecycleAction> actions)
    {
        if (actions is null)
        {
            throw new ArgumentNullException(nameof(actions));
        }

        // The source generator emits the complete hierarchy order (base-to-derived
        // setup and derived-to-base teardown). Preserve that order here: sorting only
        // by stage/order would lose the inheritance boundary needed by the runner.
        Actions = new ReadOnlyCollection<GeneratedLifecycleAction>(new List<GeneratedLifecycleAction>(actions));
    }

    public IReadOnlyList<GeneratedLifecycleAction> Actions { get; }

    public bool IsEmpty => Actions.Count == 0;
}

/// <summary>
/// The lifecycle scope represented by a generated action.
/// </summary>
public enum GeneratedLifecycleStage
{
    ClassSetup,
    TestSetup,
    TestTeardown,
    ClassTeardown,
}
