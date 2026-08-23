using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;

namespace Bicep.Extension.Storage.Handlers;

public sealed class DataLakeDirectoryHandler : StorageResourceHandlerBase<DataLakeDirectory, DataLakeDirectoryIdentifiers>
{
    protected override Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
    {
        // Normalize up front so that preview and deployment agree on the identifier.
        request.Properties.Path = NormalizePath(request.Properties.Path);

        return Task.FromResult(GetResponse(request));
    }

    protected override Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
        => HandleDataLakeRequest(request, async service =>
        {
            var declared = request.Properties;

            Validate(declared);

            var path = RequirePath(declared.Path);
            var directory = service.GetFileSystemClient(declared.FileSystemName).GetDirectoryClient(path);
            var metadata = ToMetadata(declared.Metadata);

            var created = await directory.CreateIfNotExistsAsync(
                new DataLakePathCreateOptions
                {
                    Metadata = metadata,
                    AccessOptions = new DataLakeAccessOptions
                    {
                        Permissions = declared.Permissions,
                        // The service masks the declared permissions with the umask, which defaults
                        // to 0027 - so a declared '0770' would silently become '0750'.
                        Umask = declared.Permissions is null ? null : "0000",
                        Owner = declared.Owner,
                        Group = declared.Group,
                    },
                },
                cancellationToken);

            // The SDK returns a null response - not a null value - when the directory exists, so
            // reconcile it instead.
            if (created is null)
            {
                await directory.SetMetadataAsync(metadata, cancellationToken: cancellationToken);

                await DataLakeAccessControl.ApplyAsync(
                    directory,
                    declared.AccessControl,
                    declared.Permissions,
                    declared.Owner,
                    declared.Group,
                    InvalidValue,
                    cancellationToken);
            }
            else if (declared.AccessControl is not null)
            {
                // Access control cannot be supplied on the create request, so apply it separately.
                await DataLakeAccessControl.ApplyAsync(
                    directory,
                    declared.AccessControl,
                    permissions: null,
                    declared.Owner,
                    declared.Group,
                    InvalidValue,
                    cancellationToken);
            }

            var result = await ReadAsync(directory, declared.FileSystemName, path, cancellationToken);

            // The service always reports both representations, but the model treats them as
            // mutually exclusive, so echo back whichever one was declared. Owner and group fall
            // back to the service values when the deployment did not set them.
            declared.Path = path;
            declared.Permissions = declared.AccessControl is not null
                ? null
                : declared.Permissions ?? result.Permissions;
            declared.Owner ??= result.Owner;
            declared.Group ??= result.Group;
            declared.Url = result.Url;
            declared.ETag = result.ETag;
            declared.LastModified = result.LastModified;

            return BuildResponse(request.Type, request.ApiVersion, declared);
        });

    protected override Task<ResourceResponse> Get(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleDataLakeRequest(request, async service =>
        {
            var path = RequirePath(request.Identifiers.Path);
            var directory = service.GetFileSystemClient(request.Identifiers.FileSystemName).GetDirectoryClient(path);

            return BuildResponse(
                request.Type,
                request.ApiVersion,
                await ReadAsync(directory, request.Identifiers.FileSystemName, path, cancellationToken));
        });

    protected override Task<ResourceResponse> Delete(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleDataLakeRequest(request, async service =>
        {
            await service
                .GetFileSystemClient(request.Identifiers.FileSystemName)
                .GetDirectoryClient(RequirePath(request.Identifiers.Path))
                .DeleteIfExistsAsync(cancellationToken: cancellationToken);

            return GetResponse(request, properties: null);
        });

    protected override DataLakeDirectoryIdentifiers GetIdentifiers(DataLakeDirectory properties)
        => new() { FileSystemName = properties.FileSystemName, Path = NormalizePath(properties.Path) };

    private static void Validate(DataLakeDirectory declared)
    {
        if (declared.AccessControl is not null && declared.Permissions is not null)
        {
            throw new ResourceErrorException(
                "ConflictingAccessControl",
                "'permissions' cannot be combined with 'accessControl'.",
                "permissions");
        }

        if (declared.AccessControl is { Length: 0 })
        {
            throw new ResourceErrorException(
                "EmptyAccessControl",
                "'accessControl' must declare at least one entry. Omit it to leave the existing access control untouched.",
                "accessControl");
        }
    }

    private static async Task<DataLakeDirectory> ReadAsync(
        DataLakeDirectoryClient directory,
        string fileSystemName,
        string path,
        CancellationToken cancellationToken)
    {
        var properties = (await directory.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        var accessControl = (await directory.GetAccessControlAsync(cancellationToken: cancellationToken)).Value;

        return new DataLakeDirectory
        {
            FileSystemName = fileSystemName,
            Path = path,
            Metadata = FromMetadata(properties.Metadata),
            AccessControl = DataLakeAccessControl.FromAccessControlList(accessControl.AccessControlList),
            Permissions = accessControl.Permissions?.ToOctalPermissions(),
            Owner = accessControl.Owner,
            Group = accessControl.Group,
            Url = directory.Uri.ToString(),
            ETag = properties.ETag.ToString(),
            LastModified = ToIso8601(properties.LastModified),
        };
    }

    private static ResourceResponse BuildResponse(string type, string? apiVersion, DataLakeDirectory properties)
        => new()
        {
            Type = type,
            ApiVersion = apiVersion,
            Properties = properties,
            Identifiers = new DataLakeDirectoryIdentifiers { FileSystemName = properties.FileSystemName, Path = properties.Path },
        };

    private static string RequirePath(string path)
        => NormalizePath(path) is { Length: > 0 } normalized
            ? normalized
            : throw new ResourceErrorException("InvalidPath", "'path' must name a directory within the file system.", "path");

    private static Exception InvalidValue(string message, string target)
        => new ResourceErrorException("InvalidAccessControl", message, target);
}
