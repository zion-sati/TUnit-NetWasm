using TUnit.Core;

namespace TUnit.UnitTests;

public sealed class GeneratedTestCaseContractTests
{
    private static int _syncInvocations;
    private static int _taskInvocations;
    private static int _valueTaskInvocations;

    [Test]
    public async Task GeneratedSourceExposesDirectInvocationContract()
    {
        var source = SourceRegistrar.GetRegisteredTestSources()
            .Single(static candidate => candidate.ClassName.EndsWith(
                nameof(GeneratedTestCaseContractTests), StringComparison.Ordinal));
        var generatedCase = source.GetGeneratedCases()
            .Single(static candidate => candidate.MethodName == nameof(GeneratedSourceExposesDirectInvocationContract));

        await Assert.That(generatedCase.InvocationKind).IsEqualTo(GeneratedInvocationKind.Task);
        await Assert.That(generatedCase).IsNotNull();
        await Assert.That(generatedCase.StableId).IsNotEmpty();
    }

    [Test]
    public async Task GeneratedSourceInvokesSyncAndGenericAsyncDelegates()
    {
        var source = SourceRegistrar.GetRegisteredTestSources()
            .Single(static candidate => candidate.ClassName.EndsWith(
                nameof(GeneratedTestCaseContractTests), StringComparison.Ordinal));
        var generatedCases = source.GetGeneratedCases();

        var syncCase = generatedCases.Single(static candidate =>
            candidate.MethodName == nameof(SyncDelegateTarget));
        var taskCase = generatedCases.Single(static candidate =>
            candidate.MethodName == nameof(GenericTaskDelegateTarget));
        var valueTaskCase = generatedCases.Single(static candidate =>
            candidate.MethodName == nameof(GenericValueTaskDelegateTarget));

        await Assert.That(syncCase.InvocationKind).IsEqualTo(GeneratedInvocationKind.Sync);
        await Assert.That(taskCase.InvocationKind).IsEqualTo(GeneratedInvocationKind.Task);
        await Assert.That(valueTaskCase.InvocationKind).IsEqualTo(GeneratedInvocationKind.ValueTask);

        var syncBefore = Volatile.Read(ref _syncInvocations);
        var taskBefore = Volatile.Read(ref _taskInvocations);
        var valueTaskBefore = Volatile.Read(ref _valueTaskInvocations);

        await syncCase.ExecuteAsync();
        await taskCase.ExecuteAsync();
        await valueTaskCase.ExecuteAsync();

        await Assert.That(Volatile.Read(ref _syncInvocations)).IsGreaterThan(syncBefore);
        await Assert.That(Volatile.Read(ref _taskInvocations)).IsGreaterThan(taskBefore);
        await Assert.That(Volatile.Read(ref _valueTaskInvocations)).IsGreaterThan(valueTaskBefore);
    }

    [Test]
    public void SyncDelegateTarget() => Interlocked.Increment(ref _syncInvocations);

    [Test]
    public Task<int> GenericTaskDelegateTarget()
    {
        Interlocked.Increment(ref _taskInvocations);
        return Task.FromResult(17);
    }

    [Test]
    public ValueTask<int> GenericValueTaskDelegateTarget()
    {
        Interlocked.Increment(ref _valueTaskInvocations);
        return ValueTask.FromResult(19);
    }
}
