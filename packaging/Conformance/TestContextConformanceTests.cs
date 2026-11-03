using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace NetWasm.TUnit.Conformance;

// Compiled into the desktop and the netwasm0.1 package consumers.
// TestContext.Current carries the running test's facts under upstream TUnit's
// names, from its test hooks through its body and its awaits.
[Arguments(7)]
public sealed class TestContextConformanceTests(int seed)
{
    private TestContext? _beforeTest;

    [Before(Test)]
    public void CaptureTheContextBeforeTheTest() => _beforeTest = TestContext.Current;

    [Test]
    [Arguments(1, "one")]
    [Arguments(2, "two")]
    [Category("context-conformance")]
    [Property("owner", "conformance")]
    [Retry(2)]
    public async Task ContextCarriesTheRunningTestsFacts(int number, string name)
    {
        var context = TestContext.Current;
        await Assert.That(context).IsNotNull();
        var metadata = context!.Metadata;
        var details = metadata.TestDetails;

        await Assert.That(metadata.TestName).IsEqualTo(nameof(ContextCarriesTheRunningTestsFacts));
        await Assert.That(metadata.DisplayName.StartsWith(metadata.TestName, StringComparison.Ordinal)).IsTrue();
        await Assert.That(details.MethodName).IsEqualTo(nameof(ContextCarriesTheRunningTestsFacts));
        await Assert.That(details.ClassType).IsEqualTo(typeof(TestContextConformanceTests));
        await Assert.That(ReferenceEquals(details.ClassInstance, this)).IsTrue();
        await Assert.That(details.TestMethodArguments.Length).IsEqualTo(2);
        await Assert.That((int)details.TestMethodArguments[0]!).IsEqualTo(number);
        await Assert.That((string)details.TestMethodArguments[1]!).IsEqualTo(name);
        await Assert.That(details.TestClassArguments.Length).IsEqualTo(1);
        await Assert.That((int)details.TestClassArguments[0]!).IsEqualTo(seed);
        await Assert.That(details.Categories.Contains("context-conformance")).IsTrue();
        await Assert.That(details.CustomProperties["owner"].Count).IsEqualTo(1);
        await Assert.That(details.CustomProperties["owner"][0]).IsEqualTo("conformance");
        await Assert.That(details.RetryLimit).IsEqualTo(2);
        await Assert.That(details.TestFilePath.EndsWith("TestContextConformanceTests.cs", StringComparison.Ordinal)).IsTrue();
        await Assert.That(ReferenceEquals(_beforeTest, context)).IsTrue();

        await Task.Yield();
        await Assert.That(ReferenceEquals(TestContext.Current, context)).IsTrue();
    }

    [Test]
    public async Task EachTestHasItsOwnContext()
    {
        await Assert.That(TestContext.Current!.Metadata.TestName).IsEqualTo(nameof(EachTestHasItsOwnContext));
        await Assert.That(TestContext.Current.Metadata.TestDetails.TestMethodArguments.Length).IsEqualTo(0);
    }
}
