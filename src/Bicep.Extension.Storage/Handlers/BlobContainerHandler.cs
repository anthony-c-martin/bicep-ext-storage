using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace Bicep.Extension.Storage.Handlers;

public sealed class BlobContainerHandler : StorageResourceHandlerBase<BlobContainer, BlobContainerIdentifiers>
{
    protected override Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
        => Task.FromResult(GetResponse(request));

    protected override Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
        => HandleBlobRequest(request, async service =>
        {
            var declared = request.Properties;
            var container = service.GetBlobContainerClient(declared.Name);
            var metadata = ToMetadata(declared.Metadata);
            var publicAccess = ToPublicAccessType(declared.PublicAccess);

            var encryptionScope = declared.DefaultEncryptionScope is null && declared.PreventEncryptionScopeOverride is null
                ? null
                : new BlobContainerEncryptionScopeOptions
                {
                    DefaultEncryptionScope = declared.DefaultEncryptionScope,
                    PreventEncryptionScopeOverride = declared.PreventEncryptionScopeOverride ?? false,
                };

            var created = await container.CreateIfNotExistsAsync(publicAccess, metadata, encryptionScope, cancellationToken);

            // The SDK returns a null response - not a null value - when the container already
            // exists, so bring it in line with the declared state instead. Encryption scope
            // settings are immutable after creation.
            if (created is null)
            {
                await container.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
                await container.SetAccessPolicyAsync(publicAccess, cancellationToken: cancellationToken);
            }

            var result = await ReadAsync(container, cancellationToken);

            if (created is null)
            {
                RejectImmutableChange(declared.Name, "defaultEncryptionScope", declared.DefaultEncryptionScope, result.DefaultEncryptionScope);
                RejectImmutableChange(declared.Name, "preventEncryptionScopeOverride", declared.PreventEncryptionScopeOverride, result.PreventEncryptionScopeOverride ?? false);
            }

            // Echo the declared state so the response matches what Bicep asked for, taking only the
            // service-assigned read-only fields from the read.
            declared.Url = result.Url;
            declared.ETag = result.ETag;
            declared.LastModified = result.LastModified;
            declared.HasImmutabilityPolicy = result.HasImmutabilityPolicy;
            declared.HasLegalHold = result.HasLegalHold;

            return BuildResponse(request.Type, request.ApiVersion, container, declared);
        });

    protected override Task<ResourceResponse> Get(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleBlobRequest(request, async service =>
        {
            var container = service.GetBlobContainerClient(request.Identifiers.Name);

            return BuildResponse(request.Type, request.ApiVersion, container, await ReadAsync(container, cancellationToken));
        });

    protected override Task<ResourceResponse> Delete(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleBlobRequest(request, async service =>
        {
            await service.GetBlobContainerClient(request.Identifiers.Name).DeleteIfExistsAsync(cancellationToken: cancellationToken);

            return GetResponse(request, properties: null);
        });

    protected override BlobContainerIdentifiers GetIdentifiers(BlobContainer properties)
        => new() { Name = properties.Name };

    private static async Task<BlobContainer> ReadAsync(BlobContainerClient container, CancellationToken cancellationToken)
    {
        var properties = (await container.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;

        return new BlobContainer
        {
            Name = container.Name,
            PublicAccess = FromPublicAccessType(properties.PublicAccess),
            Metadata = FromMetadata(properties.Metadata),
            DefaultEncryptionScope = NullIfEmpty(properties.DefaultEncryptionScope),
            PreventEncryptionScopeOverride = properties.PreventEncryptionScopeOverride,
            Url = container.Uri.ToString(),
            ETag = properties.ETag.ToString(),
            LastModified = ToIso8601(properties.LastModified),
            HasImmutabilityPolicy = properties.HasImmutabilityPolicy,
            HasLegalHold = properties.HasLegalHold,
        };
    }

    private static ResourceResponse BuildResponse(string type, string? apiVersion, BlobContainerClient container, BlobContainer properties)
        => new()
        {
            Type = type,
            ApiVersion = apiVersion,
            Properties = properties,
            Identifiers = new BlobContainerIdentifiers { Name = container.Name },
        };

    private static PublicAccessType ToPublicAccessType(BlobContainerPublicAccess? publicAccess)
        => publicAccess switch
        {
            BlobContainerPublicAccess.Blob => PublicAccessType.Blob,
            BlobContainerPublicAccess.Container => PublicAccessType.BlobContainer,
            _ => PublicAccessType.None,
        };

    private static BlobContainerPublicAccess FromPublicAccessType(PublicAccessType? publicAccess)
        => publicAccess switch
        {
            PublicAccessType.Blob => BlobContainerPublicAccess.Blob,
            PublicAccessType.BlobContainer => BlobContainerPublicAccess.Container,
            _ => BlobContainerPublicAccess.None,
        };
}
