using TUnit.Core;
using TUnit.Core.Helpers;
using TUnit.Assertions.Exceptions;

namespace TUnit.UnitTests;

/// <summary>
/// Exercises the generated catalog as a desktop semantic oracle. The assertions
/// intentionally consume only the generated contract, not reflection metadata.
/// </summary>
public sealed class GeneratedCatalogSemanticOracleTests
{
    private static int _setupCount;
    private static int _teardownCount;

    [Test]
    public async Task GeneratedSourceUsesTheCatalogContract()
    {
        var catalog = new SourceGeneratedTestCatalog(
            GetSource().GetGeneratedCases(),
            "TUnit.Core.SourceGenerator");
        var cases = catalog.GetGeneratedCases();

        await Assert.That(cases).IsNotEmpty();
        await Assert.That(cases.Select(static testCase => testCase.StableId)
                .SequenceEqual(catalog.GetGeneratedCases().Select(static testCase => testCase.StableId)))
            .IsTrue();
    }

    [Test]
    public async Task CatalogCaseProjectionDoesNotRequireRuntimeTypeNames()
    {
        var expected = CreateThrowingCase("oracle.explicit", new InvalidOperationException("name-free"));
        var catalog = new SourceGeneratedTestCatalog(
            new GeneratedTestCase[] { expected });

        var cases = catalog.GetGeneratedCases();

        await Assert.That(cases.Count).IsEqualTo(1);
        await Assert.That(cases[0].StableId).IsEqualTo(expected.StableId);
    }

    [Test]
    public async Task GeneratedCasesHaveStableOrderAndDirectDelegates()
    {
        var source = GetSource();
        var cases = source.GetGeneratedCases();

        await Assert.That(cases).IsNotEmpty();
        await Assert.That(cases.Select(static testCase => testCase.StableId).Distinct(StringComparer.Ordinal).Count())
            .IsEqualTo(cases.Count);
        await Assert.That(cases.Select(static testCase => testCase.StableId)
                .SequenceEqual(cases.Select(static testCase => testCase.StableId)
                    .OrderBy(static id => id, StringComparer.Ordinal)))
            .IsTrue();
        await Assert.That(cases.All(static testCase => testCase.CompletionPolicy == GeneratedCompletionPolicy.Await)).IsTrue();
        await Assert.That(cases.All(static testCase => testCase.CatalogProvenance == "TUnit.Core.SourceGenerator")).IsTrue();
        await Assert.That(cases.All(static testCase => testCase.Lifecycle.IsEmpty)).IsTrue();
    }

    [Test]
    public async Task GeneratedCasesAwaitSyncTaskAndValueTask()
    {
        var source = GetSource();
        var cases = source.GetGeneratedCases();

        await cases.Single(static testCase => testCase.MethodName == nameof(SyncCase))
            .InvokeAsync();
        await cases.Single(static testCase => testCase.MethodName == nameof(TaskCase))
            .InvokeAsync();
        await cases.Single(static testCase => testCase.MethodName == nameof(ValueTaskCase))
            .InvokeAsync();

    }

    [Test]
    public async Task GeneratedParameterizedCasesPreservePayloadDisplayAndIdentity()
    {
        var cases = GetSource().GetGeneratedCases()
            .Where(static testCase => testCase.MethodName == nameof(ParameterizedCase))
            .ToArray();

        await Assert.That(cases.Length).IsEqualTo(1);
        await Assert.That(cases.Select(static testCase => testCase.DisplayName)
                .SequenceEqual(new[] { nameof(ParameterizedCase) }))
            .IsTrue();
        await Assert.That(cases[0].Arguments).IsEmpty();
        await Assert.That(cases[0].StableId.StartsWith(
                cases[0].FullyQualifiedName + "#",
                StringComparison.Ordinal))
            .IsTrue();
    }

    [Test]
    public async Task DesktopDisplayNameSubstitutionDefinesTheCatalogOracle()
    {
        var parameters = new ParameterMetadata[]
        {
            new(typeof(string)) { Name = "first", TypeInfo = new ConcreteType(typeof(string)) },
            new(typeof(int)) { Name = "second", TypeInfo = new ConcreteType(typeof(int)) },
        };

        var displayName = DisplayNameSubstitutor.Substitute(
            "row $arg1/$arg2/$arg3/$arg1x/$first/$second/$firstExtra",
            parameters,
            ["x", 2]);

        await Assert.That(displayName).IsEqualTo("row x/2/$arg3/$arg1x/x/2/$firstExtra");

        var constructorDisplayName = DisplayNameSubstitutor.Substitute(
            "constructor row $arg1/$text",
            parameters,
            [3]);

        await Assert.That(constructorDisplayName).IsEqualTo("constructor row 3/$text");
    }

    [Test]
    public async Task GeneratedInvocationPreservesAssertionAndUnexpectedExceptions()
    {
        var assertion = CreateThrowingCase("oracle.assertion", new AssertionException("assertion"));
        var unexpected = CreateThrowingCase("oracle.unexpected", new InvalidOperationException("unexpected"));

        await Assert.That(async () => await assertion.InvokeAsync()).Throws<AssertionException>();
        await Assert.That(async () => await unexpected.InvokeAsync()).Throws<InvalidOperationException>();
    }

    [Test]
    public async Task GeneratedTeardownRunsWhenSetupFails()
    {
        var teardownCount = 0;
        var lifecycle = new GeneratedLifecycle(
        [
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestSetup,
                0,
                static (_, _) => new ValueTask(Task.FromException(new InvalidOperationException("setup")))),
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestTeardown,
                0,
                (_, _) =>
                {
                    Interlocked.Increment(ref teardownCount);
                    return ValueTask.CompletedTask;
                }),
        ]);
        var testCase = CreateThrowingCase("oracle.setup-failure", new InvalidOperationException("test"), lifecycle);

        await Assert.That(async () => await testCase.InvokeAsync()).Throws<InvalidOperationException>();
        await Assert.That(teardownCount).IsEqualTo(1);
    }

    [Test]
    public async Task GeneratedTeardownRunsAllHooksAndPreservesEarlierFailures()
    {
        var teardownCount = 0;
        var teardownReceivedNone = false;
        var lifecycle = new GeneratedLifecycle(
        [
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestTeardown,
                0,
                (_, cancellationToken) =>
                {
                    teardownReceivedNone |= cancellationToken == CancellationToken.None;
                    throw new InvalidOperationException("cleanup-one");
                }),
            new GeneratedLifecycleAction(
                GeneratedLifecycleStage.TestTeardown,
                1,
                (_, cancellationToken) =>
                {
                    teardownReceivedNone &= cancellationToken == CancellationToken.None;
                    Interlocked.Increment(ref teardownCount);
                    throw new InvalidOperationException("cleanup-two");
                }),
        ]);
        var testCase = CreateThrowingCase("oracle.aggregate", new InvalidOperationException("body"), lifecycle);

        Exception? observed = null;
        try
        {
            await testCase.InvokeAsync(new CancellationToken(true));
        }
        catch (Exception exception)
        {
            observed = exception;
        }

        await Assert.That(observed is AggregateException).IsTrue();
        var aggregate = (AggregateException) observed!;
        await Assert.That(aggregate.InnerExceptions.Select(static exception => exception.Message)
                .SequenceEqual(new[] { "body", "cleanup-one", "cleanup-two" }))
            .IsTrue();
        await Assert.That(teardownCount).IsEqualTo(1);
        await Assert.That(teardownReceivedNone).IsTrue();
    }

    [Before(HookType.Class)]
    public static void ClassSetUp()
    {
    }

    [After(HookType.Class)]
    public static void ClassTearDown()
    {
    }

    [Before(HookType.Test)]
    public void SetUp() => Interlocked.Increment(ref _setupCount);

    [After(HookType.Test)]
    public void TearDown() => Interlocked.Increment(ref _teardownCount);

    [Test]
    public void SyncCase()
    {
    }

    [Test]
    public Task TaskCase() => Task.CompletedTask;

    [Test]
    public ValueTask ValueTaskCase() => ValueTask.CompletedTask;

    [Test]
    [Arguments(7, "unused", DisplayName = "first")]
    [Arguments(3, "unused", DisplayName = "second")]
    public void ParameterizedCase(int value, string label)
    {
        if (value is not (7 or 3) || label != "unused")
        {
            throw new InvalidOperationException("Generated arguments were not supplied.");
        }
    }

    private static ITestEntrySource GetSource() => SourceRegistrar.GetRegisteredTestSources()
        .Single(static source => source.ClassName.EndsWith(
            nameof(GeneratedCatalogSemanticOracleTests), StringComparison.Ordinal));

    private static GeneratedTestCase CreateThrowingCase(
        string stableId,
        Exception exception,
        GeneratedLifecycle? lifecycle = null)
    {
        return new GeneratedTestCase<OracleTarget>(
            "Throws",
            "oracle.Throws",
            "oracle",
            "oracle.cs",
            1,
            GeneratedInvocationKind.Sync,
            static () => new OracleTarget(),
            (_, _) => new ValueTask(Task.FromException(exception)),
            [],
            [],
            [],
            new GeneratedTestCaseRow(stableId, "Throws"),
            lifecycle ?? GeneratedLifecycle.Empty,
            GeneratedCompletionPolicy.Await,
            "semantic-oracle");
    }

    private sealed class OracleTarget
    {
    }

}

file static class GeneratedTestCaseExtensions
{
    public static async ValueTask InvokeAsync(this GeneratedTestCase testCase, CancellationToken cancellationToken = default)
    {
        await testCase.ExecuteAsync(cancellationToken);
    }
}
