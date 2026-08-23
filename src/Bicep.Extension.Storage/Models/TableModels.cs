using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace Bicep.Extension.Storage.Models;

public class TableIdentifiers
{
    [TypeProperty("The table name", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string Name { get; set; }
}

[ResourceType("Table")]
public class Table : TableIdentifiers
{
    [TypeProperty("Stored access policies for the table. Managing these requires 'accountKey' authentication, because the table ACL API does not accept Microsoft Entra tokens.")]
    public TableAccessPolicy[]? AccessPolicies { get; set; }

    [TypeProperty("The table URL", ObjectTypePropertyFlags.ReadOnly)]
    public string? Url { get; set; }
}

public class TableAccessPolicy
{
    [TypeProperty("The signed identifier of the policy", ObjectTypePropertyFlags.Required)]
    public required string Id { get; set; }

    [TypeProperty("The permissions granted by the policy, as a combination of 'r' (query), 'a' (add), 'u' (update) and 'd' (delete)", ObjectTypePropertyFlags.Required)]
    public required string Permissions { get; set; }

    [TypeProperty("When the policy becomes valid, as an ISO 8601 timestamp")]
    public string? Start { get; set; }

    [TypeProperty("When the policy expires, as an ISO 8601 timestamp")]
    public string? Expiry { get; set; }
}

/// <summary>
/// The OData EDM types supported for table entity properties.
/// </summary>
public enum TableEntityValueType
{
    String,
    Boolean,
    Int32,
    Int64,
    Double,
    DateTime,
    Guid,
    Binary,
}

public class TableEntityIdentifiers
{
    [TypeProperty("The containing table name", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string TableName { get; set; }

    [TypeProperty("The entity partition key", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string PartitionKey { get; set; }

    [TypeProperty("The entity row key", ObjectTypePropertyFlags.Identifier | ObjectTypePropertyFlags.Required)]
    public required string RowKey { get; set; }
}

[ResourceType("TableEntity")]
public class TableEntity : TableEntityIdentifiers
{
    [TypeProperty("The entity properties. Values are written as 'Edm.String' unless overridden in 'entityTypes'.", ObjectTypePropertyFlags.Required)]
    public required Dictionary<string, string> Entity { get; set; }

    [TypeProperty("Overrides the EDM type used for individual entity properties. Values in 'entity' are parsed according to the type named here.")]
    public Dictionary<string, TableEntityValueType>? EntityTypes { get; set; }

    [TypeProperty("The entity ETag", ObjectTypePropertyFlags.ReadOnly)]
    public string? ETag { get; set; }

    [TypeProperty("When the entity was last modified", ObjectTypePropertyFlags.ReadOnly)]
    public string? Timestamp { get; set; }
}
