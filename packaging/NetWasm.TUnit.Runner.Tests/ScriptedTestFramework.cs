using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Requests;

namespace NetWasm.TUnit.Runner.Tests;

// A test framework that answers each request with scripted test nodes, as TUnit's engine publishes them, and records
// what the host asked for.
internal sealed class ScriptedTestFramework : ITestFramework, IDataProducer
{
    private readonly Func<IRequest, IEnumerable<TestNode>> _nodes;

    public ScriptedTestFramework(Func<IRequest, IEnumerable<TestNode>> nodes, bool sessionSucceeds = true)
    {
        _nodes = nodes;
        SessionSucceeds = sessionSucceeds;
    }

    public bool SessionSucceeds { get; }

    public List<IRequest> Requests { get; } = [];

    public bool SessionClosed { get; private set; }

    public string Uid => "scripted";
    public string Version => "1.0.0";
    public string DisplayName => "Scripted";
    public string Description => "Scripted";

    public Type[] DataTypesProduced => [typeof(TestNodeUpdateMessage)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) =>
        Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

    public async Task ExecuteRequestAsync(ExecuteRequestContext context)
    {
        Requests.Add(context.Request);
        try
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            foreach (var node in _nodes(context.Request))
            {
                await context.MessageBus.PublishAsync(this, new TestNodeUpdateMessage(context.Request.Session.SessionUid, node));
            }
        }
        finally
        {
            context.Complete();
        }
    }

    public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context)
    {
        SessionClosed = true;
        return Task.FromResult(new CloseTestSessionResult { IsSuccess = SessionSucceeds });
    }

    public static TestNode Node(string uid, params IProperty[] properties) =>
        new() { Uid = new TestNodeUid(uid), DisplayName = uid, Properties = new PropertyBag(properties) };
}
