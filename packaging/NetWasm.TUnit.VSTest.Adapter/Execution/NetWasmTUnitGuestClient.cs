using System.Collections.Immutable;
using System.Text;
using NetWasm.Hosting.Execution;
using NetWasm.TUnit.VSTest.Adapter.Protocol;

namespace NetWasm.TUnit.VSTest.Adapter.Execution;

internal sealed class NetWasmTUnitGuestClient(
    INetWasmArtifactClient artifacts,
    ITUnitProtocolReader protocol) : ITUnitGuestClient
{
    private readonly INetWasmArtifactClient _artifacts =
        artifacts ?? throw new ArgumentNullException(nameof(artifacts));
    private readonly ITUnitProtocolReader _protocol =
        protocol ?? throw new ArgumentNullException(nameof(protocol));

    public async ValueTask<TUnitGuestRun> RunAsync(
        string source,
        ImmutableArray<string> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        if (arguments.IsDefault)
        {
            throw new ArgumentException("TUnit guest arguments must be explicit.", nameof(arguments));
        }

        await using var standardOutput = new MemoryStream();
        await using var standardError = new MemoryStream();
        var execution = await _artifacts.ExecuteAsync(
            new NetWasmArtifactExecutionRequest(source, arguments),
            standardOutput,
            standardError,
            cancellationToken).ConfigureAwait(false);

        var output = Encoding.UTF8.GetString(standardOutput.ToArray());
        var error = Encoding.UTF8.GetString(standardError.ToArray());
        var session = execution.CompletionKind == NetWasmCompletionKind.Normal
            ? _protocol.Read(output)
            : null;
        return new TUnitGuestRun(execution, session, output, error);
    }
}
