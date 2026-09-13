using NetWasm.TUnit.Runner.Model;

namespace NetWasm.TUnit.Runner.Reporting;

public interface ITestEventSink
{
    void Write(TestRunEvent testEvent);
}
