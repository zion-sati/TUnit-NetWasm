using System.Reflection;

namespace TUnit.Core.Helpers;

/// <summary>
/// A type's assembly and declaring type, as the engine groups and names tests by them. NetWasm keeps no type-owner
/// metadata at run time: there the test program's own assembly stands for a test type's assembly, and test types are
/// taken as top-level.
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

    public static Type? DeclaringType(Type type)
    {
#if NETWASM
        // NetWasm does not supply Type.DeclaringType yet; switch to it when it does.
        return null;
#else
        return type.DeclaringType;
#endif
    }
}
