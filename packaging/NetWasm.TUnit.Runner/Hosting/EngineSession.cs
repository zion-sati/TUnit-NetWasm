using System.Runtime.CompilerServices;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.TestHost;

namespace NetWasm.TUnit.Runner.Hosting;

/// <summary>
/// Runs one request against a test framework as Microsoft.Testing.Platform's test host would: create the session,
/// execute the request with the host's message bus, wait for the framework to report completion, then close the
/// session, which disposes session-scoped state. The platform creates sessions and their contexts itself, so their
/// constructors are reached directly.
/// </summary>
public sealed class EngineSession(ITestFramework framework)
{
    private static readonly SessionUid Uid = new("netwasm");

    /// <returns>Whether the framework reports the session as successful.</returns>
    public async Task<bool> RunAsync(
        Func<TestSessionContext, IRequest> createRequest,
        IMessageBus messageBus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(createRequest);
        ArgumentNullException.ThrowIfNull(messageBus);

        var created = await framework.CreateTestSessionAsync(CreateSessionContext(Uid, cancellationToken));
        if (!created.IsSuccess)
        {
            throw new InvalidOperationException(created.ErrorMessage ?? "The test framework did not create its session.");
        }

        var completion = new CompletionNotifier();
        await framework.ExecuteRequestAsync(new ExecuteRequestContext(
            createRequest(CreateSession(Uid)),
            messageBus,
            completion,
            cancellationToken));
        await completion.Completed;

        var closed = await framework.CloseTestSessionAsync(CloseSessionContext(Uid, cancellationToken));
        return closed.IsSuccess;
    }

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern TestSessionContext CreateSession(SessionUid sessionUid);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern CreateTestSessionContext CreateSessionContext(SessionUid sessionUid, CancellationToken cancellationToken);

    [UnsafeAccessor(UnsafeAccessorKind.Constructor)]
    private static extern CloseTestSessionContext CloseSessionContext(SessionUid sessionUid, CancellationToken cancellationToken);

    private sealed class CompletionNotifier : IExecuteRequestCompletionNotifier
    {
        private readonly TaskCompletionSource _completed = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completed => _completed.Task;

        public void Complete() => _completed.TrySetResult();
    }
}
