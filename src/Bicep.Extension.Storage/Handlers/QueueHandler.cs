using Azure.Storage.Queues;

namespace Bicep.Extension.Storage.Handlers;

public sealed class QueueHandler : StorageResourceHandlerBase<Queue, QueueIdentifiers>
{
    protected override Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
        => Task.FromResult(GetResponse(request));

    protected override Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
        => HandleQueueRequest(request, async service =>
        {
            var queue = service.GetQueueClient(request.Properties.Name);
            var metadata = ToMetadata(request.Properties.Metadata);

            var created = await queue.CreateIfNotExistsAsync(metadata, cancellationToken);

            // A null response means the queue already existed, so reconcile its metadata.
            if (created is null)
            {
                await queue.SetMetadataAsync(metadata, cancellationToken);
            }

            var result = await ReadAsync(queue, cancellationToken);

            // Echo the declared state, taking only the service-assigned read-only fields.
            var declared = request.Properties;
            declared.Url = result.Url;
            declared.ApproximateMessagesCount = result.ApproximateMessagesCount;

            return BuildResponse(request.Type, request.ApiVersion, declared);
        });

    protected override Task<ResourceResponse> Get(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleQueueRequest(request, async service =>
            BuildResponse(
                request.Type,
                request.ApiVersion,
                await ReadAsync(service.GetQueueClient(request.Identifiers.Name), cancellationToken)));

    protected override Task<ResourceResponse> Delete(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleQueueRequest(request, async service =>
        {
            await service.GetQueueClient(request.Identifiers.Name).DeleteIfExistsAsync(cancellationToken);

            return GetResponse(request, properties: null);
        });

    protected override QueueIdentifiers GetIdentifiers(Queue properties)
        => new() { Name = properties.Name };

    private static async Task<Queue> ReadAsync(QueueClient queue, CancellationToken cancellationToken)
    {
        var properties = (await queue.GetPropertiesAsync(cancellationToken)).Value;

        return new Queue
        {
            Name = queue.Name,
            Metadata = FromMetadata(properties.Metadata),
            Url = queue.Uri.ToString(),
            ApproximateMessagesCount = properties.ApproximateMessagesCount,
        };
    }

    private static ResourceResponse BuildResponse(string type, string? apiVersion, Queue properties)
        => new()
        {
            Type = type,
            ApiVersion = apiVersion,
            Properties = properties,
            Identifiers = new QueueIdentifiers { Name = properties.Name },
        };
}
