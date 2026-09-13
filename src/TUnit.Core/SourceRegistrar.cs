using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using TUnit.Core.Hooks;
using TUnit.Core.Interfaces.SourceGenerator;

namespace TUnit.Core;

#if !DEBUG
[System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
#endif
[StackTraceHidden]
/// <summary>
/// Provides methods to register various sources.
/// </summary>
public class SourceRegistrar
{
    public static bool IsEnabled { get; set; }

    /// <summary>
    /// Registers an assembly loader.
    /// </summary>
    /// <param name="assemblyLoader">The assembly loader to register.</param>
    public static void RegisterAssembly(Func<Assembly> assemblyLoader)
    {
#if NET
        if (!RuntimeFeature.IsDynamicCodeSupported)
        {
            return;
        }
#endif

        Sources.AssemblyLoaders.Enqueue(assemblyLoader);
    }

    /// <summary>
    /// Registers a dynamic test source. Returns a dummy value so it can be used as a static field
    /// initializer on the consolidated registration <c>.cctor</c>, collapsing per-source module
    /// initializers into one merged <c>.cctor</c>.
    /// </summary>
    /// <param name="testSource">The test source to register.</param>
    public static int RegisterDynamic(IDynamicTestSource testSource)
    {
        Sources.DynamicTestSources.Enqueue(testSource);
        return 0;
    }

    /// <summary>
    /// Registers a global initializer.
    /// </summary>
    /// <param name="initializer">The initializer to register.</param>
    public static void RegisterGlobalInitializer(Func<Task> initializer)
    {
        Sources.GlobalInitializers.Enqueue(initializer);
    }

    /// <summary>
    /// Registers a property source (for property injection).
    /// </summary>
    /// <param name="propertySource">The property source to register.</param>
    public static void RegisterProperty(IPropertySource propertySource)
    {
        Sources.PropertySources.Enqueue(propertySource);
    }

    /// <summary>
    /// Registers a hook factory into a type-keyed dictionary. The factory is not invoked
    /// until the engine materializes the hook for execution. Use a <c>static</c> lambda
    /// (no captures) to keep module-init cost minimal and to remain AOT compatible.
    /// Returns a dummy value for use as a static field initializer.
    /// </summary>
    public static int RegisterHook<T>(ConcurrentDictionary<Type, ConcurrentBag<LazyHookEntry<T>>> dictionary, Type key, int registrationIndex, Func<int, T> factory)
        where T : HookMethod
    {
        dictionary.GetOrAdd(key, static _ => new ConcurrentBag<LazyHookEntry<T>>())
            .Add(new LazyHookEntry<T>(registrationIndex, factory));
        return 0;
    }

    /// <summary>
    /// Registers a hook factory into an assembly-keyed dictionary. The factory is not invoked
    /// until the engine materializes the hook for execution. Use a <c>static</c> lambda
    /// (no captures) to keep module-init cost minimal and to remain AOT compatible.
    /// Returns a dummy value for use as a static field initializer.
    /// </summary>
    public static int RegisterHook<T>(ConcurrentDictionary<Assembly, ConcurrentBag<LazyHookEntry<T>>> dictionary, Assembly key, int registrationIndex, Func<int, T> factory)
        where T : HookMethod
    {
        dictionary.GetOrAdd(key, static _ => new ConcurrentBag<LazyHookEntry<T>>())
            .Add(new LazyHookEntry<T>(registrationIndex, factory));
        return 0;
    }

    /// <summary>
    /// Registers a hook factory into a global bag. The factory is not invoked until the engine
    /// materializes the hook for execution. Use a <c>static</c> lambda (no captures) to keep
    /// module-init cost minimal and to remain AOT compatible.
    /// Returns a dummy value for use as a static field initializer.
    /// </summary>
    public static int RegisterHook<T>(ConcurrentBag<LazyHookEntry<T>> bag, int registrationIndex, Func<int, T> factory)
        where T : HookMethod
    {
        bag.Add(new LazyHookEntry<T>(registrationIndex, factory));
        return 0;
    }

    /// <summary>
    /// Registers a factory for test entries. The factory is not invoked until the engine
    /// needs to access the entries (during discovery/filtering), avoiding per-class JIT
    /// compilation during module initialization.
    /// Returns a dummy value for use as a static field initializer.
    /// Multiple calls for the same T are additive — factories accumulate.
    /// </summary>
    public static int RegisterEntries<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(Func<TestEntry<T>[]> factory) where T : class
    {
        return RegisterEntriesCore(factory, generatedCasesFactory: null);
    }

    /// <summary>
    /// Registers a desktop entry factory together with its direct generated-case factory.
    /// The second factory is consumed by <see cref="ITestEntrySource.GetGeneratedCases"/>
    /// without reconstructing cases from desktop metadata. The one-factory overload remains
    /// for callers generated by older versions.
    /// </summary>
    public static int RegisterEntries<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(
        Func<TestEntry<T>[]> factory,
        Func<GeneratedTestCase[]> generatedCasesFactory) where T : class
    {
        return RegisterEntriesCore(factory, generatedCasesFactory);
    }

    private static int RegisterEntriesCore<[System.Diagnostics.CodeAnalysis.DynamicallyAccessedMembers(System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicConstructors | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicProperties | System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.PublicMethods)] T>(
        Func<TestEntry<T>[]> factory,
        Func<GeneratedTestCase[]>? generatedCasesFactory) where T : class
    {
        var key = typeof(T);

        while (true)
        {
            if (Sources.TestEntries.TryGetValue(key, out var existing))
            {
                if (existing is TestEntrySource<T> existingSource)
                {
                    existingSource.AddFactory(factory, generatedCasesFactory);
                    return 0;
                }

                throw new InvalidOperationException(
                    $"Type mismatch in TestEntries for '{typeof(T).FullName}': expected TestEntrySource<{typeof(T).Name}>, found {existing.GetType().Name}");
            }

            var source = generatedCasesFactory is null
                ? new TestEntrySource<T>(factory)
                : new TestEntrySource<T>(factory, generatedCasesFactory);
            if (Sources.TestEntries.TryAdd(key, source))
            {
                return 0;
            }

            // Another thread added between TryGetValue and TryAdd — retry to merge
        }
    }

    /// <summary>
    /// Returns a stable snapshot of the source-generated test entry sources registered
    /// by module initializers. The snapshot is read-only and preserves the desktop
    /// registration path.
    /// </summary>
    public static IReadOnlyList<ITestEntrySource> GetRegisteredTestSources()
    {
        return Array.AsReadOnly(
            Sources.TestEntries.Values
                .OrderBy(static source => source.ClassName, StringComparer.Ordinal)
                .ToArray());
    }
}
