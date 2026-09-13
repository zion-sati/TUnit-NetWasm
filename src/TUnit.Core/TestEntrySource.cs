using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace TUnit.Core;

/// <summary>
/// Thin non-generic wrapper around TestEntry&lt;T&gt;[] for engine access.
/// Wraps one or more factories that produce entries, resolving lazily on first access
/// to avoid per-class JIT compilation during module initialization.
/// </summary>
#if !DEBUG
[EditorBrowsable(EditorBrowsableState.Never)]
#endif
public sealed class TestEntrySource<
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors
        | DynamicallyAccessedMemberTypes.PublicProperties
        | DynamicallyAccessedMemberTypes.PublicMethods)] T> : ITestEntrySource where T : class
{
    private List<Func<TestEntry<T>[]>>? _factories;
    private List<Func<GeneratedTestCase[]>>? _generatedCaseFactories;
    private volatile TestEntry<T>[]? _entries;
    private volatile GeneratedTestCase[]? _generatedCases;
    private string? _className;
    private readonly object _lock = new();

    public TestEntrySource(Func<TestEntry<T>[]> factory)
    {
        var cachedFactory = CreateCachedFactory(factory);
        _factories = [cachedFactory];
        _generatedCaseFactories = [() => CreateGeneratedCases(cachedFactory)];
    }

    internal TestEntrySource(
        Func<TestEntry<T>[]> factory,
        Func<GeneratedTestCase[]> generatedCaseFactory)
    {
        _factories = [factory];
        _generatedCaseFactories = [generatedCaseFactory];
    }

    /// <summary>
    /// Adds another factory for the same T. Used when multiple source-gen files
    /// register entries for the same class (e.g. generic instantiations).
    /// Thread-safe via lock since static field initializers may run concurrently.
    /// </summary>
    internal void AddFactory(
        Func<TestEntry<T>[]> factory,
        Func<GeneratedTestCase[]>? generatedCaseFactory = null)
    {
        var cachedFactory = generatedCaseFactory is null
            ? CreateCachedFactory(factory)
            : factory;

        lock (_lock)
        {
            if (_entries is not null)
            {
                // Already resolved — merge eagerly
                var additional = cachedFactory();
                _entries = [.. _entries, .. additional];
            }
            else
            {
                _factories!.Add(cachedFactory);
            }

            if (_generatedCases is not null)
            {
                var additionalCases = (generatedCaseFactory ?? (() => CreateGeneratedCases(cachedFactory)))();
                var combinedCases = new List<GeneratedTestCase>(_generatedCases);
                combinedCases.AddRange(additionalCases);
                _generatedCases = combinedCases
                    .OrderBy(static testCase => testCase.StableId, StringComparer.Ordinal)
                    .ToArray();
                return;
            }

            _generatedCaseFactories ??= [];
            _generatedCaseFactories.Add(generatedCaseFactory ?? (() => CreateGeneratedCases(cachedFactory)));
        }
    }

    private static Func<TestEntry<T>[]> CreateCachedFactory(Func<TestEntry<T>[]> factory)
    {
        TestEntry<T>[]? cachedEntries = null;
        return () => cachedEntries ??= factory();
    }

    private static GeneratedTestCase[] CreateGeneratedCases(Func<TestEntry<T>[]> factory)
    {
        var entries = factory();
        var cases = new GeneratedTestCase[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            cases[index] = entries[index].ToGeneratedCase(index);
        }

        return cases;
    }

    private TestEntry<T>[] Resolve()
    {
        if (_entries is not null)
        {
            return _entries;
        }

        lock (_lock)
        {
            if (_entries is not null)
            {
                return _entries;
            }

            var factories = _factories!;
            TestEntry<T>[] entries;

            if (factories.Count == 1)
            {
                entries = factories[0]();
            }
            else
            {
                var allEntries = new List<TestEntry<T>>();
                foreach (var factory in factories)
                {
                    allEntries.AddRange(factory());
                }
                entries = allEntries.ToArray();
            }

            if (entries.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Source-generated test registration failed: no entries for '{typeof(T).FullName}'. " +
                    "This indicates a source generator bug. Please report this issue.");
            }

            _entries = entries;
            _factories = null;
            return entries;
        }
    }

    public int Count => Resolve().Length;
    public Type ClassType => typeof(T);
    public string ClassName => _className ??= TUnit.Core.Extensions.TestContextExtensions.GetNestedTypeName(typeof(T));

    public TestEntryFilterData GetFilterData(int index)
    {
        var entry = Resolve()[index];
        return new TestEntryFilterData
        {
            MethodName = entry.MethodName,
            ClassName = ClassName,
            Categories = entry.Categories,
            Properties = entry.Properties,
            DependsOn = entry.DependsOn,
            HasDataSource = entry.HasDataSource,
            RepeatCount = entry.RepeatCount,
        };
    }

    public IReadOnlyList<TestMetadata> Materialize(int index, string testSessionId)
    {
        return [Resolve()[index].ToTestMetadata(testSessionId)];
    }

    public IReadOnlyList<GeneratedTestCase> GetGeneratedCases()
    {
        if (_generatedCases is not null)
        {
            return _generatedCases;
        }

        lock (_lock)
        {
            if (_generatedCases is not null)
            {
                return _generatedCases;
            }

            var cases = new List<GeneratedTestCase>();
            if (_generatedCaseFactories is not null)
            {
                foreach (var factory in _generatedCaseFactories)
                {
                    cases.AddRange(factory());
                }
            }

            _generatedCases = cases
                .OrderBy(static testCase => testCase.StableId, StringComparer.Ordinal)
                .ToArray();
            _generatedCaseFactories = null;
            return _generatedCases;
        }
    }
}
