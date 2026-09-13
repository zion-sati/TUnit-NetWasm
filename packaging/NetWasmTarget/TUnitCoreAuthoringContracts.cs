using System.Runtime.CompilerServices;

namespace TUnit.Core;

public class TUnitAttribute : Attribute
{
    internal TUnitAttribute()
    {
    }
}

[AttributeUsage(AttributeTargets.Method)]
public abstract class BaseTestAttribute : TUnitAttribute
{
    internal BaseTestAttribute(string file, int line)
    {
        File = file;
        Line = line;
    }

    public string File { get; }

    public int Line { get; }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class TestAttribute(
    [CallerFilePath] string file = "",
    [CallerLineNumber] int line = 0) : BaseTestAttribute(file, line);

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class ArgumentsAttribute(params object?[]? values) : Attribute
{
    public object?[] Values { get; } = values ?? [null];

    public string? Skip { get; set; }

    public string? DisplayName { get; set; }

    public string[]? Categories { get; set; }

    public bool SkipIfEmpty { get; set; }

    public bool DeferEnumeration { get; set; }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Property | AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class ArgumentsAttribute<T>(T value) : Attribute
{
    public T Value { get; } = value;

    public string? Skip { get; set; }

    public string? DisplayName { get; set; }

    public string[]? Categories { get; set; }

    public bool SkipIfEmpty { get; set; }

    public bool DeferEnumeration { get; set; }
}

public enum HookType
{
    Test,
    Class,
    Assembly,
    TestSession,
    TestDiscovery,
}

public class HookAttribute : TUnitAttribute
{
    internal HookAttribute(HookType hookType, string file, int line)
    {
        HookType = hookType;
        File = file;
        Line = line;
    }

    public HookType HookType { get; }

    public string File { get; }

    public int Line { get; }

    public int Order { get; init; }
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class BeforeAttribute(
    HookType hookType,
    [CallerFilePath] string file = "",
    [CallerLineNumber] int line = 0) : HookAttribute(hookType, file, line);

[AttributeUsage(AttributeTargets.Method)]
public sealed class AfterAttribute(
    HookType hookType,
    [CallerFilePath] string file = "",
    [CallerLineNumber] int line = 0) : HookAttribute(hookType, file, line);

[AttributeUsage(AttributeTargets.Method)]
public sealed class BeforeEveryAttribute(
    HookType hookType,
    [CallerFilePath] string file = "",
    [CallerLineNumber] int line = 0) : HookAttribute(hookType, file, line);

[AttributeUsage(AttributeTargets.Method)]
public sealed class AfterEveryAttribute(
    HookType hookType,
    [CallerFilePath] string file = "",
    [CallerLineNumber] int line = 0) : HookAttribute(hookType, file, line);

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
public class CategoryAttribute(string category) : TUnitAttribute
{
    public string Category { get; } = category;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
public class PropertyAttribute(string name, string value) : TUnitAttribute
{
    public string Name { get; } = name;

    public string Value { get; } = value;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = false)]
public sealed class DisplayNameAttribute(string displayName) : TUnitAttribute
{
    public string DisplayName { get; } = displayName;
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class DependsOnAttribute : TUnitAttribute
{
    public DependsOnAttribute(string testName) : this(null, testName, null)
    {
    }

    public DependsOnAttribute(string testName, Type[] parameterTypes) : this(null, testName, parameterTypes)
    {
    }

    public DependsOnAttribute(Type testClass) : this(testClass, null, null)
    {
    }

    public DependsOnAttribute(Type testClass, string testName) : this(testClass, testName, null)
    {
    }

    public DependsOnAttribute(Type? testClass, string? testName, Type[]? parameterTypes)
    {
        ClassMetadata = testClass;
        TestName = testName;
        ParameterTypes = parameterTypes;
    }

    public Type? ClassMetadata { get; }

    public string? TestName { get; }

    public Type[]? ParameterTypes { get; }

    public bool ProceedOnFailure { get; set; }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = true)]
public class DependsOnAttribute<T> : DependsOnAttribute
{
    public DependsOnAttribute() : base(typeof(T))
    {
    }

    public DependsOnAttribute(string testName) : base(typeof(T), testName)
    {
    }

    public DependsOnAttribute(string testName, Type[] parameterTypes) : base(typeof(T), testName, parameterTypes)
    {
    }
}

[AttributeUsage(AttributeTargets.Class)]
public sealed class InheritsTestsAttribute : TUnitAttribute;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class GenerateGenericTestAttribute(params Type[] typeArguments) : Attribute
{
    public Type[] TypeArguments { get; } = typeArguments ?? throw new ArgumentNullException(nameof(typeArguments));
}
