using NetWasm.TUnit.Runner.Model;
using NetWasm.TUnit.Runner.Reporting;
using TUnit.Core;

namespace NetWasm.TUnit.Runner.Tests;

internal static class TestCaseFactory
{
    public static GeneratedTestCase Create(
        string stableId,
        string groupIdentity = "Tests.Group",
        GeneratedInvocationKind invocationKind = GeneratedInvocationKind.Sync,
        Func<TestInstance, CancellationToken, ValueTask>? invoke = null,
        GeneratedLifecycle? lifecycle = null)
    {
        return new GeneratedTestCase<TestInstance>(
            methodName: stableId,
            fullyQualifiedName: $"Tests.Group.{stableId}",
            groupIdentity,
            filePath: "Tests.cs",
            lineNumber: 1,
            invocationKind,
            createInstance: static () => new TestInstance(),
            invoke: invoke ?? (static (_, _) => ValueTask.CompletedTask),
            categories: [],
            properties: [],
            dependencies: [],
            row: new GeneratedTestCaseRow(stableId, stableId),
            lifecycle);
    }
}

internal sealed class TestInstance;

internal sealed class RecordingEventSink : ITestEventSink
{
    public List<TestRunEvent> Events { get; } = [];

    public void Write(TestRunEvent testEvent) => Events.Add(testEvent);
}
