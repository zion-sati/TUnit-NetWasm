namespace NetWasm.TUnit.Runner.Model;

public abstract record TestRunEvent;

public sealed record ProtocolVersionEvent(int Version) : TestRunEvent;

public sealed record RunStartedEvent(int SelectedCount) : TestRunEvent;

public sealed record TestStartedEvent(string StableId, string DisplayName) : TestRunEvent;

public sealed record TestCompletedEvent(TestCaseResult Result) : TestRunEvent;

public sealed record RunCompletedEvent(TestRunResult Result) : TestRunEvent;

public sealed record CatalogEntryEvent(
    string StableId,
    string DisplayName,
    string FullyQualifiedName,
    string FilePath,
    int LineNumber) : TestRunEvent;

public sealed record CatalogTraitEvent(string StableId, string Name, string Value) : TestRunEvent;

public sealed record HostResultEvent(int Status, string Message) : TestRunEvent;
