using System.Reflection;

namespace TUnit.Core.Helpers;

/// <summary>
/// A type's assembly, as the engine groups tests by it. A NetWasm program has no assemblies at run time, so its own
/// assembly stands for a test type's.
/// </summary>
internal static class TypeOwner
{
    public static Assembly Assembly(Type type)
    {
#if NETWASM
        return System.Reflection.Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException("A NetWasm test project runs as the program, so it has an entry assembly.");
#else
        return type.Assembly;
#endif
    }
}
