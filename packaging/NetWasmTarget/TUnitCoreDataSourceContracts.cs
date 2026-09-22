namespace TUnit.Core;

// NetWasm materializes these data sources through generated direct calls rather
// than the desktop runtime IDataSourceAttribute method. Keep the marker in the
// target profile so TUnit's analyzers and source generator still identify the
// attributes as data sources without pulling the reflection-based runtime API
// into a NetWasm test assembly. ConditionalAttribute also keeps the authoring
// metadata out of the emitted assembly after the generated catalog has captured
// its semantics.
public interface IDataSourceAttribute;

public enum SharedType
{
    None,
    PerClass,
    PerAssembly,
    PerTestSession,
    Keyed,
}

[System.Diagnostics.Conditional("NETWASM_TUNIT_PRESERVE_SOURCE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = true)]
public class MethodDataSourceAttribute : Attribute, IDataSourceAttribute
{
    public MethodDataSourceAttribute(string methodNameProvidingDataSource)
    {
        MethodNameProvidingDataSource = methodNameProvidingDataSource;
    }

    public MethodDataSourceAttribute(Type classProvidingDataSource, string methodNameProvidingDataSource)
    {
        ClassProvidingDataSource = classProvidingDataSource;
        MethodNameProvidingDataSource = methodNameProvidingDataSource;
    }

    public Type? ClassProvidingDataSource { get; }

    public string MethodNameProvidingDataSource { get; }

    public object?[] Arguments { get; set; } = [];

    public bool SkipIfEmpty { get; set; }

    public bool DeferEnumeration { get; set; }
}

[System.Diagnostics.Conditional("NETWASM_TUNIT_PRESERVE_SOURCE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class MethodDataSourceAttribute<T>(string methodNameProvidingDataSource)
    : MethodDataSourceAttribute(typeof(T), methodNameProvidingDataSource);

[System.Diagnostics.Conditional("NETWASM_TUNIT_PRESERVE_SOURCE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = true)]
public class ClassDataSourceAttribute(params Type[] types) : Attribute, IDataSourceAttribute
{
    public Type[] Types { get; } = types;

    public SharedType[] Shared { get; set; } = [SharedType.None];

    public string[] Keys { get; set; } = [];
}

[System.Diagnostics.Conditional("NETWASM_TUNIT_PRESERVE_SOURCE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class ClassDataSourceAttribute<T> : Attribute, IDataSourceAttribute
{
    public SharedType Shared { get; set; } = SharedType.None;

    public string Key { get; set; } = string.Empty;
}

[System.Diagnostics.Conditional("NETWASM_TUNIT_PRESERVE_SOURCE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ClassDataSourceAttribute<T1, T2> : Attribute, IDataSourceAttribute
    where T1 : new()
    where T2 : new()
{
    public SharedType[] Shared { get; set; } = [SharedType.None, SharedType.None];

    public string[] Keys { get; set; } = [string.Empty, string.Empty];
}

[System.Diagnostics.Conditional("NETWASM_TUNIT_PRESERVE_SOURCE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ClassDataSourceAttribute<T1, T2, T3> : Attribute, IDataSourceAttribute
    where T1 : new()
    where T2 : new()
    where T3 : new()
{
    public SharedType[] Shared { get; set; } = [SharedType.None, SharedType.None, SharedType.None];

    public string[] Keys { get; set; } = [string.Empty, string.Empty, string.Empty];
}

[System.Diagnostics.Conditional("NETWASM_TUNIT_PRESERVE_SOURCE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ClassDataSourceAttribute<T1, T2, T3, T4> : Attribute, IDataSourceAttribute
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
{
    public SharedType[] Shared { get; set; } = [SharedType.None, SharedType.None, SharedType.None, SharedType.None];

    public string[] Keys { get; set; } = [string.Empty, string.Empty, string.Empty, string.Empty];
}

[System.Diagnostics.Conditional("NETWASM_TUNIT_PRESERVE_SOURCE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class ClassDataSourceAttribute<T1, T2, T3, T4, T5> : Attribute, IDataSourceAttribute
    where T1 : new()
    where T2 : new()
    where T3 : new()
    where T4 : new()
    where T5 : new()
{
    public SharedType[] Shared { get; set; } = [SharedType.None, SharedType.None, SharedType.None, SharedType.None, SharedType.None];

    public string[] Keys { get; set; } = [string.Empty, string.Empty, string.Empty, string.Empty, string.Empty];
}
