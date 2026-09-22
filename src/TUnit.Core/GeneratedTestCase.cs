using System.Collections.ObjectModel;

namespace TUnit.Core;

/// <summary>
/// Passive metadata plus the narrow executable action shared by heterogeneous
/// generated catalogs. Concrete typed delegates live in <see cref="GeneratedTestCase{T}"/>.
/// </summary>
public abstract class GeneratedTestCase
{
    private readonly Func<ValueTask>? _disposeData;
    private bool _dataDisposed;

    protected GeneratedTestCase(
        string methodName,
        string fullyQualifiedName,
        string groupIdentity,
        string filePath,
        int lineNumber,
        GeneratedInvocationKind invocationKind,
        IEnumerable<string> categories,
        IEnumerable<string> properties,
        IEnumerable<string> dependencies,
        GeneratedTestCaseRow row,
        GeneratedLifecycle? lifecycle,
        GeneratedCompletionPolicy completionPolicy,
        string? catalogProvenance,
        TimeSpan? timeout,
        GeneratedRetryPolicy? retryPolicy,
        string? skipReason,
        int executionPriority,
        bool isExplicit,
        bool isNotDiscoverable,
        int repeatIndex,
        Func<ValueTask>? disposeData)
    {
        MethodName = Require(methodName, nameof(methodName));
        FullyQualifiedName = Require(fullyQualifiedName, nameof(fullyQualifiedName));
        GroupIdentity = Require(groupIdentity, nameof(groupIdentity));
        FilePath = Require(filePath, nameof(filePath));
        LineNumber = lineNumber;
        InvocationKind = invocationKind;
        Categories = Copy(categories, nameof(categories));
        Properties = Copy(properties, nameof(properties));
        Dependencies = Copy(dependencies, nameof(dependencies));
        Row = row ?? throw new ArgumentNullException(nameof(row));
        Lifecycle = lifecycle ?? GeneratedLifecycle.Empty;
        CompletionPolicy = completionPolicy;
        Timeout = timeout;
        RetryPolicy = retryPolicy ?? GeneratedRetryPolicy.None;
        SkipReason = skipReason;
        ExecutionPriority = executionPriority;
        IsExplicit = isExplicit;
        IsNotDiscoverable = isNotDiscoverable;
        RepeatIndex = repeatIndex;
        _disposeData = disposeData;
        CatalogProvenance = string.IsNullOrWhiteSpace(catalogProvenance)
            ? "TUnit.Core.SourceGenerator"
            : catalogProvenance!;
    }

    public string StableId => Row.StableId;

    public string MethodName { get; }

    public string DisplayName => Row.DisplayName ?? MethodName;

    public string FullyQualifiedName { get; }

    /// <summary>
    /// Compile-time identity of the generated class group. A sequential runner uses
    /// this to scope class setup and teardown around the complete group rather than
    /// repeating class hooks for each row.
    /// </summary>
    public string GroupIdentity { get; }

    public string FilePath { get; }

    public int LineNumber { get; }

    public GeneratedInvocationKind InvocationKind { get; }

    public IReadOnlyList<string> Categories { get; }

    public IReadOnlyList<string> Properties { get; }

    public IReadOnlyList<string> Dependencies { get; }

    public GeneratedTestCaseRow Row { get; }

    public IReadOnlyList<object?> Arguments => Row.Arguments;

    public IReadOnlyList<object?> ConstructorArguments => Row.ConstructorArguments;

    public GeneratedLifecycle Lifecycle { get; }

    public GeneratedCompletionPolicy CompletionPolicy { get; }

    public TimeSpan? Timeout { get; }

    public GeneratedRetryPolicy RetryPolicy { get; }

    public string? SkipReason { get; }

    public int ExecutionPriority { get; }

    public bool IsExplicit { get; }

    public bool IsNotDiscoverable { get; }

    public int RepeatIndex { get; }

    public string CatalogProvenance { get; }

    /// <summary>Runs this case through its generated, type-safe execution action.</summary>
    public abstract ValueTask ExecuteAsync(CancellationToken cancellationToken = default);

    /// <summary>Disposes case-scoped generated data after all retry attempts complete.</summary>
    public async ValueTask DisposeDataAsync()
    {
        if (_dataDisposed)
        {
            return;
        }

        _dataDisposed = true;
        if (_disposeData is not null)
        {
            await _disposeData();
        }
    }

    private static string Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Value cannot be empty.", name);
        }

        return value;
    }

    private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values, string name)
    {
        if (values is null)
        {
            throw new ArgumentNullException(name);
        }

        return new ReadOnlyCollection<T>(new List<T>(values));
    }
}

/// <summary>
/// A generated case retaining concrete create and invoke delegates. The catalog
/// exposes it as <see cref="GeneratedTestCase"/> while this type owns all typed work.
/// </summary>
public sealed class GeneratedTestCase<T> : GeneratedTestCase where T : class
{
    private readonly Func<object?[], T>? _legacyCreateInstance;
    private readonly Func<T, object?[], CancellationToken, ValueTask>? _legacyInvoke;
    private readonly Func<T>? _typedCreateInstance;
    private readonly Func<T, CancellationToken, ValueTask>? _typedInvoke;

    internal GeneratedTestCase(
        string methodName,
        string fullyQualifiedName,
        string groupIdentity,
        string filePath,
        int lineNumber,
        GeneratedInvocationKind invocationKind,
        Func<object?[], T> createInstance,
        Func<T, object?[], CancellationToken, ValueTask> invoke,
        IEnumerable<string> categories,
        IEnumerable<string> properties,
        IEnumerable<string> dependencies,
        GeneratedTestCaseRow row,
        GeneratedLifecycle? lifecycle = null,
        GeneratedCompletionPolicy completionPolicy = GeneratedCompletionPolicy.Await,
        string? catalogProvenance = null,
        TimeSpan? timeout = null,
        GeneratedRetryPolicy? retryPolicy = null,
        string? skipReason = null,
        int executionPriority = 2,
        bool isExplicit = false,
        bool isNotDiscoverable = false,
        int repeatIndex = 0,
        Func<ValueTask>? disposeData = null)
        : base(
            methodName,
            fullyQualifiedName,
            groupIdentity,
            filePath,
            lineNumber,
            invocationKind,
            categories,
            properties,
            dependencies,
            row,
            lifecycle,
            completionPolicy,
            catalogProvenance,
            timeout,
            retryPolicy,
            skipReason,
            executionPriority,
            isExplicit,
            isNotDiscoverable,
            repeatIndex,
            disposeData)
    {
        _legacyCreateInstance = createInstance ?? throw new ArgumentNullException(nameof(createInstance));
        _legacyInvoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
        _typedCreateInstance = null;
        _typedInvoke = null;
    }

    public GeneratedTestCase(
        string methodName,
        string fullyQualifiedName,
        string groupIdentity,
        string filePath,
        int lineNumber,
        GeneratedInvocationKind invocationKind,
        Func<T> createInstance,
        Func<T, CancellationToken, ValueTask> invoke,
        IEnumerable<string> categories,
        IEnumerable<string> properties,
        IEnumerable<string> dependencies,
        GeneratedTestCaseRow row,
        GeneratedLifecycle? lifecycle = null,
        GeneratedCompletionPolicy completionPolicy = GeneratedCompletionPolicy.Await,
        string? catalogProvenance = null,
        TimeSpan? timeout = null,
        GeneratedRetryPolicy? retryPolicy = null,
        string? skipReason = null,
        int executionPriority = 2,
        bool isExplicit = false,
        bool isNotDiscoverable = false,
        int repeatIndex = 0,
        Func<ValueTask>? disposeData = null)
        : base(
            methodName,
            fullyQualifiedName,
            groupIdentity,
            filePath,
            lineNumber,
            invocationKind,
            categories,
            properties,
            dependencies,
            row,
            lifecycle,
            completionPolicy,
            catalogProvenance,
            timeout,
            retryPolicy,
            skipReason,
            executionPriority,
            isExplicit,
            isNotDiscoverable,
            repeatIndex,
            disposeData)
    {
        if (createInstance is null)
        {
            throw new ArgumentNullException(nameof(createInstance));
        }

        _legacyCreateInstance = null;
        _legacyInvoke = null;
        _typedCreateInstance = createInstance;
        _typedInvoke = invoke ?? throw new ArgumentNullException(nameof(invoke));
    }

    public override async ValueTask ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var instance = _typedCreateInstance is null
            ? _legacyCreateInstance!(Row.CreateConstructorArguments())
            : _typedCreateInstance();
        // Class lifecycle is retained as catalog metadata for the sequential runner. It is
        // scoped around a class's complete case group, so executing it here would repeat
        // class setup/teardown once per row. A case owns only its test-level lifecycle.
        Exception? primaryException = null;
        Task? abandonedExecution = null;
        try
        {
            foreach (var action in Lifecycle.Actions)
            {
                if (action.Stage == GeneratedLifecycleStage.TestSetup)
                {
                    await action.InvokeAsync(instance, cancellationToken);
                }
            }

            await InvokeBodyAsync(instance, cancellationToken);
        }
        catch (Exception exception)
        {
            primaryException = exception;
            TryGetAbandonedExecution(exception, out abandonedExecution);
        }

        if (abandonedExecution is not null)
        {
            var cleanupCompletion = CompleteDeferredCleanupAsync(instance, abandonedExecution, 0);
            throw ReplaceExecutionCompletion(primaryException!, cleanupCompletion);
        }

        List<Exception>? teardownExceptions = null;
        for (var actionIndex = 0; actionIndex < Lifecycle.Actions.Count; actionIndex++)
        {
            var action = Lifecycle.Actions[actionIndex];
            if (action.Stage != GeneratedLifecycleStage.TestTeardown)
            {
                continue;
            }

            try
            {
                await action.InvokeAsync(instance, CancellationToken.None);
            }
            catch (Exception exception)
            {
                if (TryGetAbandonedExecution(exception, out var teardownExecution))
                {
                    var cleanupCompletion = CompleteDeferredCleanupAsync(
                        instance,
                        teardownExecution!,
                        actionIndex + 1);
                    (teardownExceptions ??= []).Add(ReplaceExecutionCompletion(exception, cleanupCompletion));
                    abandonedExecution = cleanupCompletion;
                    break;
                }

                (teardownExceptions ??= []).Add(exception);
            }
        }

        if (abandonedExecution is null)
        {
            await DisposeInstanceAsync(instance);
        }

        if (primaryException is not null)
        {
            if (teardownExceptions is { Count: > 0 })
            {
                var exceptions = new List<Exception>(teardownExceptions.Count + 1)
                {
                    primaryException,
                };
                exceptions.AddRange(teardownExceptions);
                throw new AggregateException(exceptions);
            }

            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(primaryException).Throw();
        }

        if (teardownExceptions is { Count: 1 })
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(teardownExceptions[0]).Throw();
        }

        if (teardownExceptions is { Count: > 1 })
        {
            throw new AggregateException(teardownExceptions);
        }

    }

    private async ValueTask InvokeBodyAsync(T instance, CancellationToken cancellationToken)
    {
        if (Timeout is not TimeSpan timeout)
        {
            await InvokeBodyCoreAsync(instance, cancellationToken);
            return;
        }

        var executionSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        Task execution;
        try
        {
            execution = InvokeBodyCoreAsync(instance, executionSource.Token).AsTask();
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
            $"Test exceeded its timeout of {timeout.TotalMilliseconds.ToString(global::System.Globalization.CultureInfo.InvariantCulture)} ms.",
            execution,
            executionStillRunning);
    }

    private ValueTask InvokeBodyCoreAsync(T instance, CancellationToken cancellationToken) =>
        _typedInvoke is not null
            ? _typedInvoke(instance, cancellationToken)
            : _legacyInvoke!(instance, Row.CreateArguments(), cancellationToken);

    private static async ValueTask DisposeInstanceAsync(T instance)
    {
        try
        {
            if (instance is IAsyncDisposable asyncDisposable)
            {
                await asyncDisposable.DisposeAsync();
            }
            else if (instance is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
        catch
        {
            // Match the desktop engine: fixture-disposal failures do not replace
            // the test outcome after lifecycle execution has completed.
        }
    }

    private static async Task ObserveAndDisposeAsync(Task execution, CancellationTokenSource source)
    {
        try
        {
            await execution;
        }
        catch
        {
            // The timeout is the reported failure. Observe a late body exception.
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
            // must not release test resources while execution still owns them.
        }
    }

    private async Task CompleteDeferredCleanupAsync(T instance, Task abandonedExecution, int teardownStartIndex)
    {
        await ObserveCleanupCompletionAsync(abandonedExecution);

        for (var actionIndex = teardownStartIndex; actionIndex < Lifecycle.Actions.Count; actionIndex++)
        {
            var action = Lifecycle.Actions[actionIndex];
            if (action.Stage != GeneratedLifecycleStage.TestTeardown)
            {
                continue;
            }

            var exception = await InvokeCleanupActionAsync(action, instance);
            if (exception is not null && TryGetAbandonedExecution(exception, out var teardownExecution))
            {
                await ObserveCleanupCompletionAsync(teardownExecution!);
            }
        }

        await DisposeInstanceAsync(instance);
    }

    private static async ValueTask<Exception?> InvokeCleanupActionAsync(
        GeneratedLifecycleAction action,
        T instance)
    {
        try
        {
            await action.InvokeAsync(instance, CancellationToken.None);
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private static async ValueTask ObserveCleanupCompletionAsync(Task execution)
    {
        try
        {
            await execution;
        }
        catch
        {
            // The original timeout or cancellation is already reported.
        }
    }

    private static bool TryGetAbandonedExecution(Exception exception, out Task? execution)
    {
        if (exception is GeneratedTimeoutException { ExecutionWasAbandoned: true } timeout)
        {
            execution = timeout.ExecutionCompletion;
            return true;
        }

        if (exception is GeneratedCancellationException { ExecutionWasAbandoned: true } cancellation)
        {
            execution = cancellation.ExecutionCompletion;
            return true;
        }

        execution = null;
        return false;
    }

    private static Exception ReplaceExecutionCompletion(Exception exception, Task executionCompletion) =>
        exception switch
        {
            GeneratedTimeoutException timeout => new GeneratedTimeoutException(
                timeout.Message,
                executionCompletion,
                executionWasAbandoned: true),
            GeneratedCancellationException cancellation => new GeneratedCancellationException(
                cancellation.CancellationToken,
                executionCompletion,
                executionWasAbandoned: true),
            _ => exception,
        };
}
