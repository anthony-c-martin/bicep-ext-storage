using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using BlobType = Bicep.Extension.Storage.Models.BlobType;

namespace Bicep.Extension.Storage.Handlers;

public sealed class BlobHandler : StorageResourceHandlerBase<Blob, BlobIdentifiers>
{
    private const int PageAlignment = 512;

    protected override Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
        => Task.FromResult(GetResponse(request));

    protected override Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
        => HandleBlobRequest(request, async service =>
        {
            var declared = request.Properties;
            var container = service.GetBlobContainerClient(declared.ContainerName);
            var headers = ToHttpHeaders(declared);
            var metadata = ToMetadata(declared.Metadata);
            var tags = declared.Tags is { Count: > 0 } ? new Dictionary<string, string>(declared.Tags) : null;

            if (declared.SourceUri is not null)
            {
                await CopyAsync(container, declared, headers, metadata, tags, cancellationToken);
            }
            else
            {
                await UploadAsync(container, declared, headers, metadata, tags, cancellationToken);
            }

            var blob = container.GetBlobClient(declared.Name);

            if (declared.AccessTier is { } accessTier)
            {
                await blob.SetAccessTierAsync(ToAccessTier(accessTier), cancellationToken: cancellationToken);
            }

            var result = await ReadAsync(container, declared.Name, cancellationToken);

            // Echo the declared state so the response matches what Bicep asked for - content is
            // write-only, and unset headers would otherwise come back as service defaults. Only the
            // service-assigned read-only fields are taken from the read.
            declared.Type = declared.Type ?? result.Type;
            declared.Url = result.Url;
            declared.ETag = result.ETag;
            declared.LastModified = result.LastModified;
            declared.VersionId = result.VersionId;

            return BuildResponse(request.Type, request.ApiVersion, declared);
        });

    protected override Task<ResourceResponse> Get(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleBlobRequest(request, async service =>
        {
            var container = service.GetBlobContainerClient(request.Identifiers.ContainerName);

            return BuildResponse(
                request.Type,
                request.ApiVersion,
                await ReadAsync(container, request.Identifiers.Name, cancellationToken));
        });

    protected override Task<ResourceResponse> Delete(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleBlobRequest(request, async service =>
        {
            await service
                .GetBlobContainerClient(request.Identifiers.ContainerName)
                .GetBlobClient(request.Identifiers.Name)
                .DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);

            return GetResponse(request, properties: null);
        });

    protected override BlobIdentifiers GetIdentifiers(Blob properties)
        => new() { ContainerName = properties.ContainerName, Name = properties.Name };

    private static async Task CopyAsync(
        BlobContainerClient container,
        Blob declared,
        BlobHttpHeaders headers,
        IDictionary<string, string> metadata,
        IDictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        if (declared.Content is not null || declared.ContentBase64 is not null)
        {
            throw new ResourceErrorException(
                "ConflictingContent",
                "'sourceUri' cannot be combined with 'content' or 'contentBase64'.",
                "sourceUri");
        }

        var blob = WithEncryptionScope(container.GetBlobClient(declared.Name), declared.EncryptionScope);
        var source = ParseAbsoluteUri(declared.SourceUri!, "sourceUri");

        await blob.SyncCopyFromUriAsync(
            source,
            new BlobCopyFromUriOptions { Metadata = metadata, Tags = tags },
            cancellationToken);

        if (HasHttpHeaders(declared))
        {
            await blob.SetHttpHeadersAsync(headers, cancellationToken: cancellationToken);
        }
    }

    private static async Task UploadAsync(
        BlobContainerClient container,
        Blob declared,
        BlobHttpHeaders headers,
        IDictionary<string, string> metadata,
        IDictionary<string, string>? tags,
        CancellationToken cancellationToken)
    {
        var content = ResolveContent(declared.Content, declared.ContentBase64, "content");

        switch (declared.Type ?? BlobType.Block)
        {
            case BlobType.Block:
            {
                var blob = WithEncryptionScope(container.GetBlockBlobClient(declared.Name), declared.EncryptionScope);
                using var stream = new MemoryStream(content, writable: false);

                await blob.UploadAsync(
                    stream,
                    new BlobUploadOptions { HttpHeaders = headers, Metadata = metadata, Tags = tags },
                    cancellationToken);
                break;
            }

            case BlobType.Append:
            {
                var blob = WithEncryptionScope(container.GetAppendBlobClient(declared.Name), declared.EncryptionScope);

                // Recreate so the blob always reflects the declared content rather than appending
                // to whatever a previous deployment left behind.
                await blob.CreateAsync(
                    new AppendBlobCreateOptions { HttpHeaders = headers, Metadata = metadata, Tags = tags },
                    cancellationToken);

                if (content.Length > 0)
                {
                    using var stream = new MemoryStream(content, writable: false);
                    await blob.AppendBlockAsync(stream, cancellationToken: cancellationToken);
                }

                break;
            }

            case BlobType.Page:
            {
                if (declared.SizeInMb is not { } sizeInMb || sizeInMb <= 0)
                {
                    throw new ResourceErrorException(
                        "MissingSize",
                        "'sizeInMb' is required and must be greater than zero for page blobs.",
                        "sizeInMb");
                }

                if (content.Length % PageAlignment != 0)
                {
                    throw new ResourceErrorException(
                        "UnalignedContent",
                        $"Page blob content must be a multiple of {PageAlignment} bytes, but the supplied content is {content.Length} bytes.",
                        "content");
                }

                var size = (long)sizeInMb * 1024 * 1024;
                if (content.Length > size)
                {
                    throw new ResourceErrorException(
                        "ContentTooLarge",
                        $"The supplied content is {content.Length} bytes, which exceeds the declared page blob size of {size} bytes.",
                        "content");
                }

                var blob = WithEncryptionScope(container.GetPageBlobClient(declared.Name), declared.EncryptionScope);

                await blob.CreateAsync(
                    size,
                    new PageBlobCreateOptions { HttpHeaders = headers, Metadata = metadata, Tags = tags },
                    cancellationToken);

                if (content.Length > 0)
                {
                    using var stream = new MemoryStream(content, writable: false);
                    await blob.UploadPagesAsync(stream, offset: 0, cancellationToken: cancellationToken);
                }

                break;
            }
        }
    }

    private static async Task<Blob> ReadAsync(BlobContainerClient container, string name, CancellationToken cancellationToken)
    {
        var blob = container.GetBlobClient(name);
        var properties = (await blob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        var tags = (await blob.GetTagsAsync(cancellationToken: cancellationToken)).Value.Tags;

        return new Blob
        {
            ContainerName = container.Name,
            Name = name,
            Type = FromBlobType(properties.BlobType),
            ContentType = NullIfEmpty(properties.ContentType),
            ContentEncoding = NullIfEmpty(properties.ContentEncoding),
            ContentLanguage = NullIfEmpty(properties.ContentLanguage),
            ContentDisposition = NullIfEmpty(properties.ContentDisposition),
            CacheControl = NullIfEmpty(properties.CacheControl),
            AccessTier = FromAccessTier(properties.AccessTier),
            Metadata = FromMetadata(properties.Metadata),
            Tags = tags is { Count: > 0 } ? new Dictionary<string, string>(tags) : null,
            Url = blob.Uri.ToString(),
            ETag = properties.ETag.ToString(),
            LastModified = ToIso8601(properties.LastModified),
            VersionId = NullIfEmpty(properties.VersionId),
        };
    }

    private static ResourceResponse BuildResponse(string type, string? apiVersion, Blob properties)
        => new()
        {
            Type = type,
            ApiVersion = apiVersion,
            Properties = properties,
            Identifiers = new BlobIdentifiers { ContainerName = properties.ContainerName, Name = properties.Name },
        };

    private static T WithEncryptionScope<T>(T blob, string? encryptionScope)
        where T : BlobBaseClient
        => encryptionScope is null ? blob : (T)blob.WithEncryptionScope(encryptionScope);

    private static bool HasHttpHeaders(Blob declared)
        => declared.ContentType is not null
            || declared.ContentEncoding is not null
            || declared.ContentLanguage is not null
            || declared.ContentDisposition is not null
            || declared.CacheControl is not null;

    private static BlobHttpHeaders ToHttpHeaders(Blob declared)
        => new()
        {
            ContentType = declared.ContentType,
            ContentEncoding = declared.ContentEncoding,
            ContentLanguage = declared.ContentLanguage,
            ContentDisposition = declared.ContentDisposition,
            CacheControl = declared.CacheControl,
        };

    private static AccessTier ToAccessTier(BlobAccessTier accessTier)
        => accessTier switch
        {
            BlobAccessTier.Cool => AccessTier.Cool,
            BlobAccessTier.Cold => AccessTier.Cold,
            BlobAccessTier.Archive => AccessTier.Archive,
            _ => AccessTier.Hot,
        };

    private static BlobAccessTier? FromAccessTier(string? accessTier)
        => accessTier switch
        {
            "Hot" => BlobAccessTier.Hot,
            "Cool" => BlobAccessTier.Cool,
            "Cold" => BlobAccessTier.Cold,
            "Archive" => BlobAccessTier.Archive,
            _ => null,
        };

    private static BlobType FromBlobType(Azure.Storage.Blobs.Models.BlobType blobType)
        => blobType switch
        {
            Azure.Storage.Blobs.Models.BlobType.Append => BlobType.Append,
            Azure.Storage.Blobs.Models.BlobType.Page => BlobType.Page,
            _ => BlobType.Block,
        };
}
