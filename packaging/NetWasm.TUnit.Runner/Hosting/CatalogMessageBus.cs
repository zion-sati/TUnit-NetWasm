using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Messages;
using NetWasm.TUnit.Runner.Model;
using NetWasm.TUnit.Runner.Reporting;

namespace NetWasm.TUnit.Runner.Hosting;

/// <summary>
/// Writes each test the engine discovers as a catalog record with its traits: categories as TestCategory and
/// custom properties as Property name=value, as the VSTest adapter maps them. A [Property] with an empty value reads
/// as a category, as TUnit's discovery publishes both the same way.
/// </summary>
public sealed class CatalogMessageBus(ITestEventSink sink) : IMessageBus
{
    private readonly HashSet<string> _listed = new(StringComparer.Ordinal);

    public int ListedCount => _listed.Count;

    public Task PublishAsync(IDataProducer dataProducer, IData data)
    {
        if (data is not TestNodeUpdateMessage { TestNode: var node }
            || node.Properties.SingleOrDefault<TestNodeStateProperty>() is not DiscoveredTestNodeStateProperty
            || !_listed.Add(node.Uid.Value))
        {
            return Task.CompletedTask;
        }

        var location = node.Properties.SingleOrDefault<TestFileLocationProperty>();
        var fullyQualifiedName = TestNodeNames.GetFullyQualifiedName(node);
        sink.Write(new CatalogEntryEvent(
            node.Uid.Value,
            node.DisplayName,
            fullyQualifiedName,
            string.IsNullOrEmpty(location?.FilePath) ? fullyQualifiedName : location.FilePath,
            location?.LineSpan.Start.Line ?? 0));

        // TUnit publishes a category as metadata with no value, and a [Property] as its name and value.
        foreach (var metadata in node.Properties.OfType<TestMetadataProperty>())
        {
            sink.Write(metadata.Value.Length == 0
                ? new CatalogTraitEvent(node.Uid.Value, "TestCategory", metadata.Key)
                : new CatalogTraitEvent(node.Uid.Value, "Property", $"{metadata.Key}={metadata.Value}"));
        }

        return Task.CompletedTask;
    }
}
