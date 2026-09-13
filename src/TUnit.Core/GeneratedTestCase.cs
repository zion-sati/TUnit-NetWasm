using System.Collections.ObjectModel;

namespace TUnit.Core;

/// <summary>
/// Passive metadata plus the narrow executable action shared by heterogeneous
/// generated catalogs. Concrete typed delegates live in <see cref="GeneratedTestCase{T}"/>.
/// </summary>
public abstract class GeneratedTestCase
{
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
        string? catalogProvenance)
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

    public string CatalogProvenance { get; }

    /// <summary>Runs this case through its generated, type-safe execution action.</summary>
    public abstract ValueTask ExecuteAsync(CancellationToken cancellationToken = default);

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
        string? catalogProvenance = null)
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
            catalogProvenance)
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
        string? catalogProvenance = null)
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
            catalogProvenance)
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
        try
        {
            foreach (var action in Lifecycle.Actions)
            {
                if (action.Stage == GeneratedLifecycleStage.TestSetup)
                {
                    await action.Invoke(instance, cancellationToken);
                }
            }

            if (_typedInvoke is not null)
            {
                await _typedInvoke(instance, cancellationToken);
            }
            else
            {
                await _legacyInvoke!(instance, Row.CreateArguments(), cancellationToken);
            }
        }
        catch (Exception exception)
        {
            primaryException = exception;
        }

        List<Exception>? teardownExceptions = null;
        foreach (var action in Lifecycle.Actions)
        {
            if (action.Stage != GeneratedLifecycleStage.TestTeardown)
            {
                continue;
            }

            try
            {
                await action.Invoke(instance, CancellationToken.None);
            }
            catch (Exception exception)
            {
                (teardownExceptions ??= []).Add(exception);
            }
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
}
