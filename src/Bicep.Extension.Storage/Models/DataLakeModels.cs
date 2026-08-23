using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace Bicep.Extension.Storage.Models;

public enum DataLakeAccessControlScope
{
    Access,
    Default,
}

public enum DataLakeAccessControlType
{
    User,
    Group,
    Mask,
    Other,
}

public class DataLakeAccessControlEntry
{
    [TypeProperty("Whether the entry applies to the item itself ('Access') or is inherited by new child items ('Default'). Defaults to 'Access'.")]
    public DataLakeAccessControlScope? Scope { get; set; }

    [TypeProperty("The type of principal the entry applies to", ObjectTypePropertyFlags.Required)]
    public required DataLakeAccessControlType Type { get; set; }

    [TypeProperty("The Microsoft Entra object ID of the user or group. Omit to target the owning user or group, and leave unset for 'Mask' and 'Other' entries.")]
    public string? Id { get; set; }

    [TypeProperty("The permissions granted, as a 3-character rwx string such as 'r-x'", ObjectTypePropertyFlags.Required)]
    public required string Permissions { get; set; }
}

public class DataLakeFileSystemIdentifiers
{
    [TypeProperty("The file system name", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string Name { get; set; }
}

[ResourceType("DataLakeFileSystem")]
public class DataLakeFileSystem : DataLakeFileSystemIdentifiers
{
    [TypeProperty("Name/value pairs to associate with the file system")]
    public Dictionary<string, string>? Metadata { get; set; }

    [TypeProperty("Access control entries applied to the file system root. Requires a hierarchical namespace-enabled account.")]
    public DataLakeAccessControlEntry[]? AccessControl { get; set; }

    [TypeProperty("The Microsoft Entra object ID of the owning user of the file system root")]
    public string? Owner { get; set; }

    [TypeProperty("The Microsoft Entra object ID of the owning group of the file system root")]
    public string? Group { get; set; }

    [TypeProperty("The encryption scope applied by default to paths in the file system. Can only be set when the file system is created.")]
    public string? DefaultEncryptionScope { get; set; }

    [TypeProperty("The file system URL", ObjectTypePropertyFlags.ReadOnly)]
    public string? Url { get; set; }

    [TypeProperty("The file system ETag", ObjectTypePropertyFlags.ReadOnly)]
    public string? ETag { get; set; }

    [TypeProperty("When the file system was last modified", ObjectTypePropertyFlags.ReadOnly)]
    public string? LastModified { get; set; }
}

public class DataLakeDirectoryIdentifiers
{
    [TypeProperty("The containing file system name", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string FileSystemName { get; set; }

    [TypeProperty("The directory path within the file system", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string Path { get; set; }
}

[ResourceType("DataLakeDirectory")]
public class DataLakeDirectory : DataLakeDirectoryIdentifiers
{
    [TypeProperty("Name/value pairs to associate with the directory")]
    public Dictionary<string, string>? Metadata { get; set; }

    [TypeProperty("Access control entries applied to the directory. Requires a hierarchical namespace-enabled account.")]
    public DataLakeAccessControlEntry[]? AccessControl { get; set; }

    [TypeProperty("POSIX permissions for the directory, as an octal string such as '0750'. Mutually exclusive with 'accessControl'.")]
    public string? Permissions { get; set; }

    [TypeProperty("The Microsoft Entra object ID of the owning user of the directory")]
    public string? Owner { get; set; }

    [TypeProperty("The Microsoft Entra object ID of the owning group of the directory")]
    public string? Group { get; set; }

    [TypeProperty("The directory URL", ObjectTypePropertyFlags.ReadOnly)]
    public string? Url { get; set; }

    [TypeProperty("The directory ETag", ObjectTypePropertyFlags.ReadOnly)]
    public string? ETag { get; set; }

    [TypeProperty("When the directory was last modified", ObjectTypePropertyFlags.ReadOnly)]
    public string? LastModified { get; set; }
}
