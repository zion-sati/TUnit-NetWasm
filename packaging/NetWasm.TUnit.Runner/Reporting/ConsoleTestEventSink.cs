using System.Text;
using NetWasm.TUnit.Runner.Model;

namespace NetWasm.TUnit.Runner.Reporting;

public sealed class ConsoleTestEventSink : ITestEventSink
{
    private readonly Action<string> _writeLine;

    public ConsoleTestEventSink(Action<string>? writeLine = null)
    {
        _writeLine = writeLine ?? Console.WriteLine;
    }

    public void Write(TestRunEvent testEvent)
    {
        ArgumentNullException.ThrowIfNull(testEvent);

        switch (testEvent)
        {
            case ProtocolVersionEvent protocol:
                WriteRecord("protocol-version", Format(protocol.Version));
                break;
            case RunStartedEvent started:
                WriteRecord("run-started", Format(started.SelectedCount));
                break;
            case TestStartedEvent started:
                WriteRecord("test-started", started.StableId, started.DisplayName);
                break;
            case TestCompletedEvent completed:
                WriteRecord(
                    "test-completed",
                    completed.Result.StableId,
                    Format(completed.Result.Outcome),
                    Format(completed.Result.Duration),
                    completed.Result.Message);
                break;
            case RunCompletedEvent completed:
                WriteRecord(
                    "run-completed",
                    Format(completed.Result.PassedCount),
                    Format(completed.Result.FailedCount),
                    Format(completed.Result.Duration));
                break;
            case CatalogEntryEvent catalog:
                WriteRecord(
                    "catalog",
                    catalog.StableId,
                    catalog.DisplayName,
                    catalog.FullyQualifiedName,
                    catalog.FilePath,
                    Format(catalog.LineNumber));
                break;
            case CatalogTraitEvent trait:
                WriteRecord("trait", trait.StableId, trait.Name, trait.Value);
                break;
            case HostResultEvent host:
                WriteRecord("host-result", Format(host.Status), host.Message);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(testEvent));
        }
    }

    private void WriteRecord(string kind, params string?[] fields)
    {
        var record = new StringBuilder("NWTUNIT|");
        record.Append(Escape(kind));
        foreach (var field in fields)
        {
            record.Append('|');
            record.Append(Escape(field));
        }

        _writeLine(record.ToString());
    }

    private static string Format(int value) => value.ToString(global::System.Globalization.CultureInfo.InvariantCulture);

    private static string Format(TestOutcome outcome) => outcome switch
    {
        TestOutcome.Passed => "passed",
        TestOutcome.Skipped => "skipped",
        TestOutcome.AssertionFailed => "assertion-failed",
        TestOutcome.UnexpectedFailure => "unexpected-failure",
        TestOutcome.Unsupported => "unsupported",
        TestOutcome.Cancelled => "cancelled",
        TestOutcome.TimedOut => "timed-out",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
    };

    private static string Format(TimeSpan duration) => duration.TotalMilliseconds.ToString(
        "F3",
        global::System.Globalization.CultureInfo.InvariantCulture);

    private static string Escape(string? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        var escaped = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            escaped.Append(character switch
            {
                '%' => "%25",
                '|' => "%7C",
                '\r' => "%0D",
                '\n' => "%0A",
                _ => character.ToString(),
            });
        }

        return escaped.ToString();
    }
}
