using System.Globalization;
using Azure;
using Azure.Data.Tables;

namespace Bicep.Extension.Storage.Handlers;

public sealed class TableEntityHandler : StorageResourceHandlerBase<Models.TableEntity, TableEntityIdentifiers>
{
    private static readonly string[] SystemProperties = ["PartitionKey", "RowKey", "Timestamp", "odata.etag"];

    protected override Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
        => Task.FromResult(GetResponse(request));

    protected override Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
        => HandleTableRequest(request, async service =>
        {
            var declared = request.Properties;
            var table = service.GetTableClient(declared.TableName);

            var upserted = await table.UpsertEntityAsync(
                ToEntity(declared),
                TableUpdateMode.Replace,
                cancellationToken);

            // Echo the declared entity rather than the service's formatting of it - a Double
            // written as '2500.50' comes back as '2500.5', which would read as a spurious diff.
            declared.ETag = upserted.Headers.ETag?.ToString();
            declared.Timestamp = upserted.Headers.Date is { } date ? ToIso8601(date) : null;

            return BuildResponse(request.Type, request.ApiVersion, declared);
        });

    protected override Task<ResourceResponse> Get(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleTableRequest(request, async service =>
        {
            var identifiers = request.Identifiers;
            var table = service.GetTableClient(identifiers.TableName);

            return BuildResponse(
                request.Type,
                request.ApiVersion,
                await ReadAsync(table, identifiers.TableName, identifiers.PartitionKey, identifiers.RowKey, cancellationToken));
        });

    protected override Task<ResourceResponse> Delete(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleTableRequest(request, async service =>
        {
            await service
                .GetTableClient(request.Identifiers.TableName)
                .DeleteEntityAsync(request.Identifiers.PartitionKey, request.Identifiers.RowKey, ETag.All, cancellationToken);

            return GetResponse(request, properties: null);
        });

    protected override TableEntityIdentifiers GetIdentifiers(Models.TableEntity properties)
        => new()
        {
            TableName = properties.TableName,
            PartitionKey = properties.PartitionKey,
            RowKey = properties.RowKey,
        };

    private static async Task<Models.TableEntity> ReadAsync(
        TableClient table,
        string tableName,
        string partitionKey,
        string rowKey,
        CancellationToken cancellationToken)
    {
        var entity = (await table.GetEntityAsync<Azure.Data.Tables.TableEntity>(partitionKey, rowKey, cancellationToken: cancellationToken)).Value;

        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var types = new Dictionary<string, TableEntityValueType>(StringComparer.Ordinal);

        foreach (var (name, value) in entity.Where(pair => !SystemProperties.Contains(pair.Key, StringComparer.Ordinal)))
        {
            values[name] = FormatValue(value);

            if (ClassifyValue(value) is { } valueType)
            {
                types[name] = valueType;
            }
        }

        return new Models.TableEntity
        {
            TableName = tableName,
            PartitionKey = partitionKey,
            RowKey = rowKey,
            Entity = values,
            EntityTypes = types.Count > 0 ? types : null,
            ETag = entity.ETag.ToString(),
            Timestamp = entity.Timestamp is { } timestamp ? ToIso8601(timestamp) : null,
        };
    }

    private static Azure.Data.Tables.TableEntity ToEntity(Models.TableEntity declared)
    {
        var entity = new Azure.Data.Tables.TableEntity(declared.PartitionKey, declared.RowKey);

        foreach (var (name, value) in declared.Entity)
        {
            if (SystemProperties.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                throw new ResourceErrorException(
                    "ReservedProperty",
                    $"'{name}' is a reserved table entity property and cannot be set through 'entity'.",
                    "entity");
            }

            var valueType = declared.EntityTypes?.GetValueOrDefault(name, TableEntityValueType.String) ?? TableEntityValueType.String;
            entity[name] = ParseValue(name, value, valueType);
        }

        return entity;
    }

    private static object ParseValue(string name, string value, TableEntityValueType valueType)
    {
        object? parsed = valueType switch
        {
            TableEntityValueType.String => value,
            TableEntityValueType.Boolean => bool.TryParse(value, out var boolean) ? boolean : null,
            TableEntityValueType.Int32 => int.TryParse(value, CultureInfo.InvariantCulture, out var int32) ? int32 : null,
            TableEntityValueType.Int64 => long.TryParse(value, CultureInfo.InvariantCulture, out var int64) ? int64 : null,
            TableEntityValueType.Double => double.TryParse(value, CultureInfo.InvariantCulture, out var number) ? number : null,
            TableEntityValueType.DateTime => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateTime) ? dateTime : null,
            TableEntityValueType.Guid => System.Guid.TryParse(value, out var guid) ? guid : null,
            TableEntityValueType.Binary => DecodeBase64(value, "entity"),
            _ => value,
        };

        return parsed ?? throw new ResourceErrorException(
            "InvalidEntityValue",
            $"The value of '{name}' is not a valid '{valueType}'.",
            "entity");
    }

    private static string FormatValue(object? value)
        => value switch
        {
            null => string.Empty,
            byte[] binary => Convert.ToBase64String(binary),
            BinaryData binary => Convert.ToBase64String(binary.ToArray()),
            DateTimeOffset dateTime => ToIso8601(dateTime),
            DateTime dateTime => ToIso8601(dateTime),
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };

    private static TableEntityValueType? ClassifyValue(object? value)
        => value switch
        {
            byte[] or BinaryData => TableEntityValueType.Binary,
            DateTimeOffset or DateTime => TableEntityValueType.DateTime,
            bool => TableEntityValueType.Boolean,
            int => TableEntityValueType.Int32,
            long => TableEntityValueType.Int64,
            double => TableEntityValueType.Double,
            Guid => TableEntityValueType.Guid,
            _ => null,
        };

    private static ResourceResponse BuildResponse(string type, string? apiVersion, Models.TableEntity result)
        => new()
        {
            Type = type,
            ApiVersion = apiVersion,
            Properties = result,
            Identifiers = new TableEntityIdentifiers
            {
                TableName = result.TableName,
                PartitionKey = result.PartitionKey,
                RowKey = result.RowKey,
            },
        };
}
