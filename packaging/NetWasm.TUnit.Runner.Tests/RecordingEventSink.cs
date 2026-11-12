using NetWasm.TUnit.Runner.Model;
using NetWasm.TUnit.Runner.Reporting;

namespace NetWasm.TUnit.Runner.Tests;

internal sealed class RecordingEventSink : ITestEventSink
{
    public List<TestRunEvent> Events { get; } = [];

    public void Write(TestRunEvent testEvent) => Events.Add(testEvent);
}
