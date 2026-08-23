using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace Bicep.Extension.Storage.Models;

public enum BlobContainerPublicAccess
{
    None,
    Blob,
    Container,
}

public class BlobContainerIdentifiers
{
    [TypeProperty("The container name", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string Name { get; set; }
}

[ResourceType("BlobContainer")]
public class BlobContainer : BlobContainerIdentifiers
{
    [TypeProperty("The level of anonymous read access to the container. Defaults to 'None'.")]
    public BlobContainerPublicAccess? PublicAccess { get; set; }

    [TypeProperty("Name/value pairs to associate with the container")]
    public Dictionary<string, string>? Metadata { get; set; }

    [TypeProperty("The encryption scope applied by default to blobs in the container. Can only be set when the container is created.")]
    public string? DefaultEncryptionScope { get; set; }

    [TypeProperty("Whether blobs in the container are prevented from overriding the default encryption scope. Can only be set when the container is created.")]
    public bool? PreventEncryptionScopeOverride { get; set; }

    [TypeProperty("The container URL", ObjectTypePropertyFlags.ReadOnly)]
    public string? Url { get; set; }

    [TypeProperty("The container ETag", ObjectTypePropertyFlags.ReadOnly)]
    public string? ETag { get; set; }

    [TypeProperty("When the container was last modified", ObjectTypePropertyFlags.ReadOnly)]
    public string? LastModified { get; set; }

    [TypeProperty("Whether the container has an immutability policy", ObjectTypePropertyFlags.ReadOnly)]
    public bool? HasImmutabilityPolicy { get; set; }

    [TypeProperty("Whether the container has a legal hold", ObjectTypePropertyFlags.ReadOnly)]
    public bool? HasLegalHold { get; set; }
}

public enum BlobType
{
    Block,
    Page,
    Append,
}

public enum BlobAccessTier
{
    Hot,
    Cool,
    Cold,
    Archive,
}

public class BlobIdentifiers
{
    [TypeProperty("The containing blob container name", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string ContainerName { get; set; }

    [TypeProperty("The blob name, which may include '/' separators", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string Name { get; set; }
}

[ResourceType("Blob")]
public class Blob : BlobIdentifiers
{
    [TypeProperty("The blob type. Defaults to 'Block'.")]
    public BlobType? Type { get; set; }

    [TypeProperty("The blob content, as a UTF-8 string. Mutually exclusive with 'contentBase64' and 'sourceUri'.")]
    public string? Content { get; set; }

    [TypeProperty("The blob content, base64-encoded. Use with Bicep's loadFileAsBase64() to upload a local file. Mutually exclusive with 'content' and 'sourceUri'.")]
    public string? ContentBase64 { get; set; }

    [TypeProperty("A URL to copy the blob content from. The source must be publicly readable or include a SAS token. Mutually exclusive with 'content' and 'contentBase64'.")]
    public string? SourceUri { get; set; }

    [TypeProperty("The size of a page blob in MiB. Required for 'Page' blobs, and ignored otherwise.")]
    public int? SizeInMb { get; set; }

    [TypeProperty("The blob content type")]
    public string? ContentType { get; set; }

    [TypeProperty("The blob content encoding")]
    public string? ContentEncoding { get; set; }

    [TypeProperty("The blob content language")]
    public string? ContentLanguage { get; set; }

    [TypeProperty("The blob content disposition")]
    public string? ContentDisposition { get; set; }

    [TypeProperty("The blob cache control header")]
    public string? CacheControl { get; set; }

    [TypeProperty("The blob access tier")]
    public BlobAccessTier? AccessTier { get; set; }

    [TypeProperty("The encryption scope to encrypt the blob with")]
    public string? EncryptionScope { get; set; }

    [TypeProperty("Name/value pairs to associate with the blob")]
    public Dictionary<string, string>? Metadata { get; set; }

    [TypeProperty("Blob index tags, used to categorize and find blobs")]
    public Dictionary<string, string>? Tags { get; set; }

    [TypeProperty("The blob URL", ObjectTypePropertyFlags.ReadOnly)]
    public string? Url { get; set; }

    [TypeProperty("The blob ETag", ObjectTypePropertyFlags.ReadOnly)]
    public string? ETag { get; set; }

    [TypeProperty("When the blob was last modified", ObjectTypePropertyFlags.ReadOnly)]
    public string? LastModified { get; set; }

    [TypeProperty("The blob version ID, when versioning is enabled on the account", ObjectTypePropertyFlags.ReadOnly)]
    public string? VersionId { get; set; }
}
