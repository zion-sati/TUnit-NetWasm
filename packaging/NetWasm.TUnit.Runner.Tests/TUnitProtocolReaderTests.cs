using NetWasm.TUnit.VSTest.Adapter.Protocol;

namespace NetWasm.TUnit.Runner.Tests;

public sealed class TUnitProtocolReaderTests
{
    [Fact]
    public void ReadPreservesUserOutputAndDecodesVersionTwoCatalog()
    {
        var reader = new TUnitProtocolReader();

        var session = reader.Read(
            "before | text\r\n"
            + "NWTUNIT|protocol-version|2\n"
            + "NWTUNIT|catalog|case%7C1|Display%25Name|Tests.Case|Tests.cs|42\r\n"
            + "NWTUNIT|trait|case%7C1|TestCategory|fast\n"
            + "after");

        var testCase = XunitAssert.Single(session.Catalog);
        XunitAssert.Equal("case|1", testCase.StableId);
        XunitAssert.Equal("Display%Name", testCase.DisplayName);
        XunitAssert.Equal("Tests.Case", testCase.FullyQualifiedName);
        XunitAssert.Equal("Tests.cs", testCase.FilePath);
        XunitAssert.Equal(42, testCase.LineNumber);
        var trait = XunitAssert.Single(testCase.Traits);
        XunitAssert.Equal("TestCategory", trait.Name);
        XunitAssert.Equal("fast", trait.Value);
        XunitAssert.Equal("before | text\r\nafter", session.UserOutput);
    }

    [Fact]
    public void ReadAcceptsFinalMachineRecordWithoutNewline()
    {
        var session = new TUnitProtocolReader().Read(
            "NWTUNIT|protocol-version|2\n"
            + "NWTUNIT|test-started|case|Display\n"
            + "NWTUNIT|test-completed|case|assertion-failed|1.250|line 1%0Aline 2");

        XunitAssert.Equal("case", XunitAssert.Single(session.StartedStableIds));
        var completed = XunitAssert.Single(session.CompletedCases);
        XunitAssert.Equal("assertion-failed", completed.Outcome);
        XunitAssert.Equal(TimeSpan.FromMilliseconds(1.25), completed.Duration);
        XunitAssert.Equal("line 1\nline 2", completed.Message);
    }

    [Theory]
    [InlineData("NWTUNIT|catalog|case|Display|Tests.Case|Tests.cs|42")]
    [InlineData("NWTUNIT|protocol-version|1")]
    [InlineData("NWTUNIT|protocol-version|2\nNWTUNIT|unknown|value")]
    [InlineData("NWTUNIT|protocol-version|2\nNWTUNIT|catalog|case|Display|Tests.Case|Tests.cs|42\nNWTUNIT|trait|other|Name|Value")]
    [InlineData("NWTUNIT|protocol-version|2\nNWTUNIT|host-result|0|ok\nNWTUNIT|host-result|0|ok")]
    public void ReadRejectsInvalidContracts(string output)
    {
        XunitAssert.Throws<InvalidDataException>(() => new TUnitProtocolReader().Read(output));
    }
}
