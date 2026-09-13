using TUnit.Core;

namespace TUnit.UnitTests;

public sealed class GeneratedTestCatalogTests
{
    [Test]
    public async Task CatalogSnapshotsCasesAndSortsStableIds()
    {
        var first = CreateCase("z-case");
        var second = CreateCase("a-case");
        var third = CreateCase("m-case");
        var registered = new List<GeneratedTestCase> { first, second, third };
        var catalog = new SourceGeneratedTestCatalog(registered);

        registered.Add(CreateCase("b-case"));

        var cases = catalog.GetGeneratedCases();
        await Assert.That(cases.Select(static testCase => testCase.StableId)
                .SequenceEqual(["a-case", "m-case", "z-case"]))
            .IsTrue();
        await Assert.That(cases.Count).IsEqualTo(3);
    }

    [Test]
    public async Task CatalogRejectsNullCases()
    {
        await Assert.That(() => new SourceGeneratedTestCatalog(null!))
            .Throws<ArgumentNullException>();
    }

    [Test]
    public async Task CatalogRejectsDuplicateStableIds()
    {
        var duplicate = new[] { CreateCase("same"), CreateCase("same") };

        await Assert.That(() => new SourceGeneratedTestCatalog(duplicate))
            .Throws<ArgumentException>();
    }

    [Test]
    public async Task RegisteredDirectCaseFactoryAppendsAfterSnapshotResolution()
    {
        SourceRegistrar.RegisterEntries<LateFactoryTarget>(
            static () => [],
            static () => [CreateCase("late-z")]);

        var source = SourceRegistrar.GetRegisteredTestSources()
            .Single(static candidate => candidate.ClassType == typeof(LateFactoryTarget));
        var initial = source.GetGeneratedCases();

        SourceRegistrar.RegisterEntries<LateFactoryTarget>(
            static () => [],
            static () => [CreateCase("late-a")]);

        var afterLateRegistration = source.GetGeneratedCases();
        await Assert.That(initial.Select(static testCase => testCase.StableId).SequenceEqual(["late-z"]))
            .IsTrue();
        await Assert.That(afterLateRegistration.Select(static testCase => testCase.StableId)
                .SequenceEqual(["late-a", "late-z"]))
            .IsTrue();
    }

    [Test]
    public async Task LegacyFactorySharesOneCachedResultAcrossEntryAndCaseResolution()
    {
        var calls = 0;
        var source = new TestEntrySource<LegacyFactoryTarget>(() =>
        {
            calls++;
            return [CreateEntry<LegacyFactoryTarget>("legacy")];
        });

        var cases = source.GetGeneratedCases();
        var entryCount = source.Count;
        var secondRead = source.GetGeneratedCases();

        await Assert.That(calls).IsEqualTo(1);
        await Assert.That(entryCount).IsEqualTo(1);
        await Assert.That(secondRead).IsSameReferenceAs(cases);
    }

    [Test]
    public async Task LegacyFactoryLateRegistrationCachesEachFactoryOnce()
    {
        var initialCalls = 0;
        var lateCalls = 0;
        SourceRegistrar.RegisterEntries<LateLegacyFactoryTarget>(() =>
        {
            initialCalls++;
            return [CreateEntry<LateLegacyFactoryTarget>("legacy-initial")];
        });

        var source = SourceRegistrar.GetRegisteredTestSources()
            .Single(static candidate => candidate.ClassType == typeof(LateLegacyFactoryTarget));
        _ = source.GetGeneratedCases();

        SourceRegistrar.RegisterEntries<LateLegacyFactoryTarget>(() =>
        {
            lateCalls++;
            return [CreateEntry<LateLegacyFactoryTarget>("legacy-late")];
        });

        var cases = source.GetGeneratedCases();
        var entryCount = source.Count;

        await Assert.That(initialCalls).IsEqualTo(1);
        await Assert.That(lateCalls).IsEqualTo(1);
        await Assert.That(entryCount).IsEqualTo(2);
        await Assert.That(cases.Select(static testCase => testCase.MethodName)
                .SequenceEqual(["legacy-initial", "legacy-late"]))
            .IsTrue();
    }

    private static TestEntry<T> CreateEntry<T>(string methodName) where T : class
    {
        var classMetadata = new ClassMetadata
        {
            Type = typeof(T),
            TypeInfo = new ConcreteType(typeof(T)),
            Name = typeof(T).Name,
            Namespace = typeof(T).Namespace ?? string.Empty,
            Assembly = new AssemblyMetadata { Name = typeof(T).Assembly.GetName().Name ?? string.Empty },
            Parent = null,
            Parameters = [],
            Properties = [],
        };
        var methodMetadata = MethodMetadataFactory.Create(methodName, typeof(T), typeof(void), classMetadata);
        return TestEntryFactory.Create(
            methodName,
            $"{typeof(T).FullName}.{methodName}",
            "generated-catalog-tests.cs",
            1,
            methodMetadata,
            static (_, _) => Activator.CreateInstance<T>(),
            static (_, _, _, _) => ValueTask.CompletedTask,
            0,
            static _ => [],
            0);
    }

    private static GeneratedTestCase CreateCase(string stableId)
    {
        return new GeneratedTestCase<CatalogTarget>(
            "Test",
            stableId,
            "catalog",
            "catalog.cs",
            1,
            GeneratedInvocationKind.Sync,
            static () => new CatalogTarget(),
            static (_, _) => default,
            [],
            [],
            [],
            new GeneratedTestCaseRow(stableId, null));
    }

    private sealed class CatalogTarget
    {
    }

    public sealed class LateFactoryTarget
    {
    }

    public sealed class LegacyFactoryTarget
    {
    }

    public sealed class LateLegacyFactoryTarget
    {
    }
}
