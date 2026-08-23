using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace Bicep.Extension.Storage.Models;

public class QueueIdentifiers
{
    [TypeProperty("The queue name", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string Name { get; set; }
}

[ResourceType("Queue")]
public class Queue : QueueIdentifiers
{
    [TypeProperty("Name/value pairs to associate with the queue")]
    public Dictionary<string, string>? Metadata { get; set; }

    [TypeProperty("The queue URL", ObjectTypePropertyFlags.ReadOnly)]
    public string? Url { get; set; }

    [TypeProperty("The approximate number of messages in the queue", ObjectTypePropertyFlags.ReadOnly)]
    public int? ApproximateMessagesCount { get; set; }
}
