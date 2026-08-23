using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace Bicep.Extension.Storage.Handlers;

public sealed class FileShareDirectoryHandler : StorageResourceHandlerBase<FileShareDirectory, FileShareDirectoryIdentifiers>
{
    protected override Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
    {
        // Normalize up front so that preview and deployment agree on the identifier.
        request.Properties.Path = NormalizePath(request.Properties.Path);

        return Task.FromResult(GetResponse(request));
    }

    protected override Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
        => HandleShareRequest(request, async service =>
        {
            var declared = request.Properties;
            var path = RequirePath(declared.Path);
            var directory = service.GetShareClient(declared.ShareName).GetDirectoryClient(path);
            var metadata = ToMetadata(declared.Metadata);

            var created = await directory.CreateIfNotExistsAsync(
                new ShareDirectoryCreateOptions { Metadata = metadata },
                cancellationToken);

            // The SDK returns a null response - not a null value - when the directory exists.
            if (created is null)
            {
                await directory.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
            }

            var result = await ReadAsync(directory, declared.ShareName, path, cancellationToken);

            declared.Path = path;
            declared.Url = result.Url;
            declared.ETag = result.ETag;
            declared.LastModified = result.LastModified;

            return BuildResponse(request.Type, request.ApiVersion, declared.ShareName, path, declared);
        });

    protected override Task<ResourceResponse> Get(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleShareRequest(request, async service =>
        {
            var path = RequirePath(request.Identifiers.Path);
            var directory = service.GetShareClient(request.Identifiers.ShareName).GetDirectoryClient(path);

            return BuildResponse(
                request.Type,
                request.ApiVersion,
                request.Identifiers.ShareName,
                path,
                await ReadAsync(directory, request.Identifiers.ShareName, path, cancellationToken));
        });

    protected override Task<ResourceResponse> Delete(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleShareRequest(request, async service =>
        {
            await service
                .GetShareClient(request.Identifiers.ShareName)
                .GetDirectoryClient(RequirePath(request.Identifiers.Path))
                .DeleteIfExistsAsync(cancellationToken);

            return GetResponse(request, properties: null);
        });

    protected override FileShareDirectoryIdentifiers GetIdentifiers(FileShareDirectory properties)
        => new() { ShareName = properties.ShareName, Path = NormalizePath(properties.Path) };

    private static async Task<FileShareDirectory> ReadAsync(
        ShareDirectoryClient directory,
        string shareName,
        string path,
        CancellationToken cancellationToken)
    {
        var properties = (await directory.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;

        return new FileShareDirectory
        {
            ShareName = shareName,
            Path = path,
            Metadata = FromMetadata(properties.Metadata),
            Url = directory.Uri.ToString(),
            ETag = properties.ETag.ToString(),
            LastModified = ToIso8601(properties.LastModified),
        };
    }

    private static ResourceResponse BuildResponse(string type, string? apiVersion, string shareName, string path, FileShareDirectory properties)
        => new()
        {
            Type = type,
            ApiVersion = apiVersion,
            Properties = properties,
            Identifiers = new FileShareDirectoryIdentifiers { ShareName = shareName, Path = path },
        };

    private static string RequirePath(string path)
        => NormalizePath(path) is { Length: > 0 } normalized
            ? normalized
            : throw new ResourceErrorException("InvalidPath", "'path' must name a directory within the share.", "path");
}
