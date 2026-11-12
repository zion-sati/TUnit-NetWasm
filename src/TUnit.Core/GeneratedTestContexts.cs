#if NETWASM
using System.Collections.Concurrent;
using System.Reflection;
using TUnit.Core.Services;

namespace TUnit.Core;

/// <summary>
/// Creates upstream's contexts for the cases the source-generated catalog's runner executes on NetWasm, where
/// TUnit.Engine does not run: one discovery, session and assembly context for the process, a class context per test
/// class, and a <see cref="TestContext"/> per case that carries what the catalog records about it. The test project is
/// the program, so its assembly is the entry assembly.
/// </summary>
internal static class GeneratedTestContexts
{
    private static readonly IReadOnlyDictionary<Type, IReadOnlyList<Attribute>> NoAttributes =
        new Dictionary<Type, IReadOnlyList<Attribute>>();

    private static readonly Lazy<AssemblyHookContext> AssemblyContext = new(CreateAssemblyContext);

    private static readonly ConcurrentDictionary<Type, ClassHookContext> ClassContexts = new();

    public static TestContext Create(GeneratedTestCase testCase, Type classType, object classInstance, CancellationToken cancellationToken)
    {
        var classContext = ClassContexts.GetOrAdd(
            classType,
            static type => new ClassHookContext(AssemblyContext.Value) { ClassType = type });
        var returnType = testCase.InvocationKind switch
        {
            GeneratedInvocationKind.Task => typeof(Task),
            GeneratedInvocationKind.ValueTask => typeof(ValueTask),
            _ => typeof(void),
        };
        var method = MethodMetadataFactory.Create(testCase.MethodName, classType, returnType, GetClassMetadata(classType));
        var details = new TestDetails([])
        {
            TestId = testCase.StableId,
            TestName = testCase.MethodName,
            ClassType = classType,
            MethodName = testCase.MethodName,
            ClassInstance = classInstance,
            TestMethodArguments = [.. testCase.Arguments],
            TestClassArguments = [.. testCase.ConstructorArguments],
            MethodMetadata = method,
            ReturnType = returnType,
            AttributesByType = NoAttributes,
            Timeout = testCase.Timeout,
            RetryLimit = testCase.RetryPolicy.MaxRetries,
            TestFilePath = testCase.FilePath,
            TestLineNumber = testCase.LineNumber,
        };
        details.Categories.AddRange(testCase.Categories);
        foreach (var property in testCase.Properties)
        {
            // The catalog records each [Property] as name=value.
            var separator = property.IndexOf('=');
            var name = property[..separator];
            if (!details.CustomProperties.TryGetValue(name, out var values))
            {
                details.CustomProperties[name] = values = [];
            }

            values.Add(property[(separator + 1)..]);
        }

        return new TestContext(
            testCase.MethodName,
            new TestServiceProvider(),
            classContext,
            new TestBuilderContext { TestMetadata = method },
            cancellationToken)
        {
            TestDetails = details,
            CustomDisplayName = testCase.DisplayName,
        };
    }

    private static ClassMetadata GetClassMetadata(Type classType) =>
        ClassMetadata.GetOrAdd(classType.FullName ?? classType.Name, () => new ClassMetadata
        {
            Type = classType,
            TypeInfo = new ConcreteType(classType),
            Name = classType.Name,
            Namespace = classType.Namespace,
            Assembly = AssemblyMetadata.GetOrAdd(AssemblyContext.Value.Assembly.GetName().Name ?? "", AssemblyContext.Value.Assembly.GetName().Name ?? ""),
            Parameters = [],
            Properties = [],
            Parent = null,
        });

    private static AssemblyHookContext CreateAssemblyContext()
    {
        var discovery = new TestDiscoveryContext(new BeforeTestDiscoveryContext { TestFilter = null }) { TestFilter = null };
        var session = new TestSessionContext(discovery) { Id = Guid.NewGuid().ToString(), TestFilter = null };
        return new AssemblyHookContext(session)
        {
            Assembly = Assembly.GetEntryAssembly()
                ?? throw new InvalidOperationException("A NetWasm test project runs as the program, so it has an entry assembly."),
        };
    }
}
#endif
