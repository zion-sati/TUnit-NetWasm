namespace TUnit.TestProject;

public class BasicTests
{
    [Test]
    public void SynchronousTest()
    {
        // Dummy method
    }

    [Test]
    public async Task AsynchronousTest()
    {
        await Task.CompletedTask;
    }

    [Test]
    public async ValueTask ValueTaskAsynchronousTest()
    {
        await new ValueTask();
    }

    [Test]
    public Task<int> GenericTaskAsynchronousTest() => Task.FromResult(42);

    [Test]
    public ValueTask<int> GenericValueTaskAsynchronousTest() => ValueTask.FromResult(43);
}
