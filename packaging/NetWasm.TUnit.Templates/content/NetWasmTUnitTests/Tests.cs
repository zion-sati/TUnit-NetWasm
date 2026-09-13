using System.Threading.Tasks;

using TUnit.Assertions;
using TUnit.Core;

namespace NetWasmTUnitTests;

public sealed class Tests
{
    [Test]
    [Category("smoke")]
    public async Task AnswerIsFortyTwo()
    {
        var answer = 6 * 7;

        await Assert.That(answer).IsEqualTo(42);
    }
}
