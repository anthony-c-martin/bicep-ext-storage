using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace Bicep.Extension.Storage.Handlers;

public sealed class FileShareFileHandler : StorageResourceHandlerBase<FileShareFile, FileShareFileIdentifiers>
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
            var file = service.GetShareClient(declared.ShareName).GetRootDirectoryClient().GetFileClient(path);
            var content = ResolveContent(declared.Content, declared.ContentBase64, "content");

            // Create always replaces the file, so the declared content is authoritative.
            await file.CreateAsync(
                content.Length,
                new ShareFileCreateOptions
                {
                    HttpHeaders = ToHttpHeaders(declared),
                    Metadata = ToMetadata(declared.Metadata),
                },
                cancellationToken: cancellationToken);

            if (content.Length > 0)
            {
                using var stream = new MemoryStream(content, writable: false);
                await file.UploadAsync(stream, cancellationToken: cancellationToken);
            }

            var result = await ReadAsync(file, declared.ShareName, path, cancellationToken);

            // Echo the declared state so the response matches what Bicep asked for - content is
            // write-only, and an unset content type would otherwise come back as the service
            // default. Only the service-assigned read-only fields are taken from the read.
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
            var file = service.GetShareClient(request.Identifiers.ShareName).GetRootDirectoryClient().GetFileClient(path);

            return BuildResponse(
                request.Type,
                request.ApiVersion,
                request.Identifiers.ShareName,
                path,
                await ReadAsync(file, request.Identifiers.ShareName, path, cancellationToken));
        });

    protected override Task<ResourceResponse> Delete(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleShareRequest(request, async service =>
        {
            await service
                .GetShareClient(request.Identifiers.ShareName)
                .GetRootDirectoryClient()
                .GetFileClient(RequirePath(request.Identifiers.Path))
                .DeleteIfExistsAsync(cancellationToken: cancellationToken);

            return GetResponse(request, properties: null);
        });

    protected override FileShareFileIdentifiers GetIdentifiers(FileShareFile properties)
        => new() { ShareName = properties.ShareName, Path = NormalizePath(properties.Path) };

    private static async Task<FileShareFile> ReadAsync(
        ShareFileClient file,
        string shareName,
        string path,
        CancellationToken cancellationToken)
    {
        var properties = (await file.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;

        return new FileShareFile
        {
            ShareName = shareName,
            Path = path,
            ContentType = NullIfEmpty(properties.ContentType),
            ContentEncoding = NullIfEmpty(FirstOrNull(properties.ContentEncoding)),
            ContentLanguage = NullIfEmpty(FirstOrNull(properties.ContentLanguage)),
            ContentDisposition = NullIfEmpty(properties.ContentDisposition),
            CacheControl = NullIfEmpty(properties.CacheControl),
            Metadata = FromMetadata(properties.Metadata),
            Url = file.Uri.ToString(),
            ETag = properties.ETag.ToString(),
            LastModified = ToIso8601(properties.LastModified),
        };
    }

    private static ResourceResponse BuildResponse(string type, string? apiVersion, string shareName, string path, FileShareFile properties)
        => new()
        {
            Type = type,
            ApiVersion = apiVersion,
            Properties = properties,
            Identifiers = new FileShareFileIdentifiers { ShareName = shareName, Path = path },
        };

    private static ShareFileHttpHeaders ToHttpHeaders(FileShareFile declared)
        => new()
        {
            ContentType = declared.ContentType,
            ContentEncoding = declared.ContentEncoding is null ? null : [declared.ContentEncoding],
            ContentLanguage = declared.ContentLanguage is null ? null : [declared.ContentLanguage],
            ContentDisposition = declared.ContentDisposition,
            CacheControl = declared.CacheControl,
        };

    private static string RequirePath(string path)
        => NormalizePath(path) is { Length: > 0 } normalized
            ? normalized
            : throw new ResourceErrorException("InvalidPath", "'path' must name a file within the share.", "path");

    private static string? FirstOrNull(IEnumerable<string>? values)
        => values?.FirstOrDefault();
}
