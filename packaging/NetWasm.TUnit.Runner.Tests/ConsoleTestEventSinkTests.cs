using NetWasm.TUnit.Runner.Model;
using NetWasm.TUnit.Runner.Reporting;

namespace NetWasm.TUnit.Runner.Tests;

public sealed class ConsoleTestEventSinkTests
{
    [Fact]
    public void WriteEmitsEscapedMachineRecordsToTheProvidedWriter()
    {
        using var writer = new StringWriter();
        var sink = new ConsoleTestEventSink(writer.WriteLine);

        sink.Write(new TestCompletedEvent(
            new TestCaseResult(
                "case|1",
                "display",
                TestOutcome.AssertionFailed,
                "line 1\nline 2")
            {
                Duration = TimeSpan.FromMilliseconds(1.25),
            }));

        XunitAssert.Equal(
            "NWTUNIT|test-completed|case%7C1|assertion-failed|1.250|line 1%0Aline 2\n",
            writer.ToString());
    }

    [Fact]
    public void WriteEmitsRunDurationAndCounts()
    {
        using var writer = new StringWriter();
        var sink = new ConsoleTestEventSink(writer.WriteLine);
        var result = new TestRunResult([
            new TestCaseResult("passed", "passed", TestOutcome.Passed, null),
            new TestCaseResult("failed", "failed", TestOutcome.UnexpectedFailure, "failure"),
        ])
        {
            Duration = TimeSpan.FromMilliseconds(2),
        };

        sink.Write(new RunCompletedEvent(result));

        XunitAssert.Equal("NWTUNIT|run-completed|1|1|2.000\n", writer.ToString());
    }

    [Fact]
    public void WriteEmitsStartedRecordsWithTheirDeclaredArity()
    {
        using var writer = new StringWriter();
        var sink = new ConsoleTestEventSink(writer.WriteLine);

        sink.Write(new RunStartedEvent(2));
        sink.Write(new TestStartedEvent("case", "Display"));

        XunitAssert.Equal(
            "NWTUNIT|run-started|2\nNWTUNIT|test-started|case|Display\n",
            writer.ToString());
    }

    [Fact]
    public void WriteEmitsCatalogAndHostResultWithTheirDeclaredArity()
    {
        using var writer = new StringWriter();
        var sink = new ConsoleTestEventSink(writer.WriteLine);

        sink.Write(new ProtocolVersionEvent(2));
        sink.Write(new CatalogEntryEvent("case", "Display", "Tests.Case", "Tests.cs", 42));
        sink.Write(new CatalogTraitEvent("case", "TestCategory", "fast"));
        sink.Write(new HostResultEvent(0, "completed"));

        XunitAssert.Equal(
            "NWTUNIT|protocol-version|2\n"
            + "NWTUNIT|catalog|case|Display|Tests.Case|Tests.cs|42\n"
            + "NWTUNIT|trait|case|TestCategory|fast\n"
            + "NWTUNIT|host-result|0|completed\n",
            writer.ToString());
    }
}
