using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace Bicep.Extension.Storage.Models;

public enum FileShareProtocol
{
    Smb,
    Nfs,
}

public enum FileShareAccessTier
{
    TransactionOptimized,
    Hot,
    Cool,
    Premium,
}

public class FileShareIdentifiers
{
    [TypeProperty("The file share name", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string Name { get; set; }
}

[ResourceType("FileShare")]
public class FileShare : FileShareIdentifiers
{
    [TypeProperty("The maximum size of the share, in GiB")]
    public int? Quota { get; set; }

    [TypeProperty("The access tier of the share")]
    public FileShareAccessTier? AccessTier { get; set; }

    [TypeProperty("The protocol to enable on the share. Defaults to 'Smb', and can only be set when the share is created.")]
    public FileShareProtocol? EnabledProtocol { get; set; }

    [TypeProperty("Name/value pairs to associate with the share")]
    public Dictionary<string, string>? Metadata { get; set; }

    [TypeProperty("The share URL", ObjectTypePropertyFlags.ReadOnly)]
    public string? Url { get; set; }

    [TypeProperty("The share ETag", ObjectTypePropertyFlags.ReadOnly)]
    public string? ETag { get; set; }

    [TypeProperty("When the share was last modified", ObjectTypePropertyFlags.ReadOnly)]
    public string? LastModified { get; set; }
}

public class FileShareDirectoryIdentifiers
{
    [TypeProperty("The containing file share name", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string ShareName { get; set; }

    [TypeProperty("The directory path within the share. Parent directories must already exist.", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string Path { get; set; }
}

[ResourceType("FileShareDirectory")]
public class FileShareDirectory : FileShareDirectoryIdentifiers
{
    [TypeProperty("Name/value pairs to associate with the directory")]
    public Dictionary<string, string>? Metadata { get; set; }

    [TypeProperty("The directory URL", ObjectTypePropertyFlags.ReadOnly)]
    public string? Url { get; set; }

    [TypeProperty("The directory ETag", ObjectTypePropertyFlags.ReadOnly)]
    public string? ETag { get; set; }

    [TypeProperty("When the directory was last modified", ObjectTypePropertyFlags.ReadOnly)]
    public string? LastModified { get; set; }
}

public class FileShareFileIdentifiers
{
    [TypeProperty("The containing file share name", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string ShareName { get; set; }

    [TypeProperty("The file path within the share. Parent directories must already exist.", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string Path { get; set; }
}

[ResourceType("FileShareFile")]
public class FileShareFile : FileShareFileIdentifiers
{
    [TypeProperty("The file content, as a UTF-8 string. Mutually exclusive with 'contentBase64'.")]
    public string? Content { get; set; }

    [TypeProperty("The file content, base64-encoded. Use with Bicep's loadFileAsBase64() to upload a local file. Mutually exclusive with 'content'.")]
    public string? ContentBase64 { get; set; }

    [TypeProperty("The file content type")]
    public string? ContentType { get; set; }

    [TypeProperty("The file content encoding")]
    public string? ContentEncoding { get; set; }

    [TypeProperty("The file content language")]
    public string? ContentLanguage { get; set; }

    [TypeProperty("The file content disposition")]
    public string? ContentDisposition { get; set; }

    [TypeProperty("The file cache control header")]
    public string? CacheControl { get; set; }

    [TypeProperty("Name/value pairs to associate with the file")]
    public Dictionary<string, string>? Metadata { get; set; }

    [TypeProperty("The file URL", ObjectTypePropertyFlags.ReadOnly)]
    public string? Url { get; set; }

    [TypeProperty("The file ETag", ObjectTypePropertyFlags.ReadOnly)]
    public string? ETag { get; set; }

    [TypeProperty("When the file was last modified", ObjectTypePropertyFlags.ReadOnly)]
    public string? LastModified { get; set; }
}
