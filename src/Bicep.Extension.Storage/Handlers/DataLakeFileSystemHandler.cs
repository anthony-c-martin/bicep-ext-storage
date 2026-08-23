using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.DataLake.Models;

namespace Bicep.Extension.Storage.Handlers;

/// <summary>
/// Shared conversions between the Bicep access control model and the Data Lake SDK.
/// </summary>
internal static class DataLakeAccessControl
{
    public static IList<PathAccessControlItem> ToAccessControlList(
        DataLakeAccessControlEntry[] entries,
        Func<string, string, Exception> invalid)
        => entries
            .Select(entry => new PathAccessControlItem(
                ToAccessControlType(entry.Type),
                ParsePermissions(entry.Permissions, invalid),
                entry.Scope == DataLakeAccessControlScope.Default,
                entry.Id))
            .ToList();

    public static DataLakeAccessControlEntry[]? FromAccessControlList(IEnumerable<PathAccessControlItem>? items)
        => items?.Select(item => new DataLakeAccessControlEntry
        {
            Scope = item.DefaultScope ? DataLakeAccessControlScope.Default : DataLakeAccessControlScope.Access,
            Type = FromAccessControlType(item.AccessControlType),
            Id = string.IsNullOrEmpty(item.EntityId) ? null : item.EntityId,
            Permissions = item.Permissions.ToSymbolicRolePermissions(),
        }).ToArray() is { Length: > 0 } result ? result : null;

    /// <summary>
    /// Applies the declared ownership and access control to a path. Owner and group are applied
    /// alongside whichever of the two access control forms was declared, so that a single request
    /// carries the full desired state.
    /// </summary>
    public static async Task ApplyAsync(
        DataLakePathClient path,
        DataLakeAccessControlEntry[]? accessControl,
        string? permissions,
        string? owner,
        string? group,
        Func<string, string, Exception> invalid,
        CancellationToken cancellationToken)
    {
        if (accessControl is { Length: > 0 })
        {
            await path.SetAccessControlListAsync(
                ToAccessControlList(accessControl, invalid),
                owner,
                group,
                cancellationToken: cancellationToken);

            return;
        }

        if (permissions is null && owner is null && group is null)
        {
            return;
        }

        // The service requires the full permission set on a set-permissions request, so preserve
        // whatever is already applied when only ownership is being changed.
        var effective = permissions is { } declared
            ? ParseOctalPermissions(declared, invalid)
            : (await path.GetAccessControlAsync(cancellationToken: cancellationToken)).Value.Permissions;

        await path.SetPermissionsAsync(effective, owner, group, cancellationToken: cancellationToken);
    }

    private static AccessControlType ToAccessControlType(DataLakeAccessControlType type)
        => type switch
        {
            DataLakeAccessControlType.Group => AccessControlType.Group,
            DataLakeAccessControlType.Mask => AccessControlType.Mask,
            DataLakeAccessControlType.Other => AccessControlType.Other,
            _ => AccessControlType.User,
        };

    private static DataLakeAccessControlType FromAccessControlType(AccessControlType type)
        => type switch
        {
            AccessControlType.Group => DataLakeAccessControlType.Group,
            AccessControlType.Mask => DataLakeAccessControlType.Mask,
            AccessControlType.Other => DataLakeAccessControlType.Other,
            _ => DataLakeAccessControlType.User,
        };

    private static RolePermissions ParsePermissions(string permissions, Func<string, string, Exception> invalid)
    {
        try
        {
            return PathAccessControlExtensions.ParseSymbolicRolePermissions(permissions);
        }
        catch (ArgumentException)
        {
            throw invalid(
                $"'{permissions}' is not a valid permissions string. Use a 3-character rwx form such as 'r-x'.",
                "accessControl");
        }
    }

    private static PathPermissions ParseOctalPermissions(string permissions, Func<string, string, Exception> invalid)
    {
        try
        {
            return PathPermissions.ParseOctalPermissions(permissions);
        }
        catch (ArgumentException)
        {
            throw invalid(
                $"'{permissions}' is not a valid octal permissions string. Use a form such as '0750'.",
                "permissions");
        }
    }
}

public sealed class DataLakeFileSystemHandler : StorageResourceHandlerBase<DataLakeFileSystem, DataLakeFileSystemIdentifiers>
{
    protected override Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
        => Task.FromResult(GetResponse(request));

    protected override Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
        => HandleDataLakeRequest(request, async service =>
        {
            var declared = request.Properties;

            if (declared.AccessControl is { Length: 0 })
            {
                throw new ResourceErrorException(
                    "EmptyAccessControl",
                    "'accessControl' must declare at least one entry. Omit it to leave the existing access control untouched.",
                    "accessControl");
            }

            var fileSystem = service.GetFileSystemClient(declared.Name);
            var metadata = ToMetadata(declared.Metadata);

            var created = await fileSystem.CreateIfNotExistsAsync(
                new DataLakeFileSystemCreateOptions
                {
                    Metadata = metadata,
                    EncryptionScopeOptions = declared.DefaultEncryptionScope is { } scope
                        ? new DataLakeFileSystemEncryptionScopeOptions { DefaultEncryptionScope = scope }
                        : null,
                },
                cancellationToken);

            // The SDK returns a null response - not a null value - when the file system exists.
            if (created is null)
            {
                await fileSystem.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
            }

            var root = fileSystem.GetDirectoryClient(string.Empty);

            await DataLakeAccessControl.ApplyAsync(
                root,
                declared.AccessControl,
                permissions: null,
                declared.Owner,
                declared.Group,
                InvalidValue,
                cancellationToken);

            var result = await ReadAsync(
                fileSystem,
                // Access control lives on the root path and is only available on hierarchical
                // namespace accounts, so it is only read back when the deployment declares it.
                readAccessControl: declared.AccessControl is not null || declared.Owner is not null || declared.Group is not null,
                cancellationToken);

            if (created is null)
            {
                RejectImmutableChange(declared.Name, "defaultEncryptionScope", declared.DefaultEncryptionScope, result.DefaultEncryptionScope);
            }

            // Echo the declared state, falling back to the service values for ownership that the
            // deployment did not set, and taking the read-only fields from the read.
            declared.Owner ??= result.Owner;
            declared.Group ??= result.Group;
            declared.DefaultEncryptionScope ??= result.DefaultEncryptionScope;
            declared.Url = result.Url;
            declared.ETag = result.ETag;
            declared.LastModified = result.LastModified;

            return BuildResponse(request.Type, request.ApiVersion, declared);
        });

    protected override Task<ResourceResponse> Get(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleDataLakeRequest(request, async service =>
            BuildResponse(
                request.Type,
                request.ApiVersion,
                await ReadAsync(service.GetFileSystemClient(request.Identifiers.Name), readAccessControl: true, cancellationToken)));

    protected override Task<ResourceResponse> Delete(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleDataLakeRequest(request, async service =>
        {
            await service.GetFileSystemClient(request.Identifiers.Name).DeleteIfExistsAsync(cancellationToken: cancellationToken);

            return GetResponse(request, properties: null);
        });

    protected override DataLakeFileSystemIdentifiers GetIdentifiers(DataLakeFileSystem properties)
        => new() { Name = properties.Name };

    private static async Task<DataLakeFileSystem> ReadAsync(
        DataLakeFileSystemClient fileSystem,
        bool readAccessControl,
        CancellationToken cancellationToken)
    {
        var properties = (await fileSystem.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;

        var accessControl = readAccessControl
            ? (await fileSystem.GetDirectoryClient(string.Empty).GetAccessControlAsync(cancellationToken: cancellationToken)).Value
            : null;

        return new DataLakeFileSystem
        {
            Name = fileSystem.Name,
            Metadata = FromMetadata(properties.Metadata),
            AccessControl = DataLakeAccessControl.FromAccessControlList(accessControl?.AccessControlList),
            Owner = accessControl?.Owner,
            Group = accessControl?.Group,
            DefaultEncryptionScope = NullIfEmpty(properties.DefaultEncryptionScope),
            Url = fileSystem.Uri.ToString(),
            ETag = properties.ETag.ToString(),
            LastModified = ToIso8601(properties.LastModified),
        };
    }

    private static ResourceResponse BuildResponse(string type, string? apiVersion, DataLakeFileSystem properties)
        => new()
        {
            Type = type,
            ApiVersion = apiVersion,
            Properties = properties,
            Identifiers = new DataLakeFileSystemIdentifiers { Name = properties.Name },
        };

    private static Exception InvalidValue(string message, string target)
        => new ResourceErrorException("InvalidAccessControl", message, target);
}
