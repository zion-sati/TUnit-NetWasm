using NetWasm.TUnit.Runner.Model;
using NetWasm.TUnit.Runner.Reporting;
using TUnit.Core;

namespace NetWasm.TUnit.Runner.Tests;

internal static class TestCaseFactory
{
    public static GeneratedTestCase Create(
        string stableId,
        string groupIdentity = "Tests.Group",
        string? methodName = null,
        GeneratedInvocationKind invocationKind = GeneratedInvocationKind.Sync,
        Func<TestInstance>? createInstance = null,
        Func<TestInstance, CancellationToken, ValueTask>? invoke = null,
        GeneratedLifecycle? lifecycle = null,
        IEnumerable<string>? dependencies = null,
        TimeSpan? timeout = null,
        GeneratedRetryPolicy? retryPolicy = null,
        string? skipReason = null,
        int executionPriority = 2,
        bool isExplicit = false,
        bool isNotDiscoverable = false,
        Func<ValueTask>? disposeData = null)
    {
        methodName ??= stableId;
        return new GeneratedTestCase<TestInstance>(
            methodName,
            fullyQualifiedName: $"{groupIdentity}.{methodName}",
            groupIdentity,
            filePath: "Tests.cs",
            lineNumber: 1,
            invocationKind,
            createInstance: createInstance ?? (static () => new TestInstance()),
            invoke: invoke ?? (static (_, _) => ValueTask.CompletedTask),
            categories: [],
            properties: [],
            dependencies: dependencies ?? [],
            row: new GeneratedTestCaseRow(stableId, stableId),
            lifecycle,
            timeout: timeout,
            retryPolicy: retryPolicy,
            skipReason: skipReason,
            executionPriority: executionPriority,
            isExplicit: isExplicit,
            isNotDiscoverable: isNotDiscoverable,
            disposeData: disposeData);
    }
}

internal class TestInstance;

internal sealed class RecordingEventSink : ITestEventSink
{
    public List<TestRunEvent> Events { get; } = [];

    public void Write(TestRunEvent testEvent) => Events.Add(testEvent);
}
