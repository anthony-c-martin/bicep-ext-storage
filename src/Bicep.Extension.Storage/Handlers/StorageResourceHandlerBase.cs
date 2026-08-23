using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Bicep.Local.Extension.Host.Handlers;

namespace Bicep.Extension.Storage.Handlers;

public abstract class StorageResourceHandlerBase<TProperties, TIdentifiers> : TypedResourceHandler<TProperties, TIdentifiers, Configuration>
    where TProperties : class
    where TIdentifiers : class
{
    // The generated Bicep types spell enum members exactly as they are declared in C#, so the
    // serializer must not camelCase them - otherwise values echoed back to Bicep would not match
    // what was declared.
    protected override JsonSerializerOptions SerializerOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    protected Task<ResourceResponse> HandleBlobRequest(ResourceBase resource, Func<BlobServiceClient, Task<ResourceResponse>> operation)
        => HandleRequest(resource, factory => operation(factory.CreateBlobServiceClient()));

    protected Task<ResourceResponse> HandleShareRequest(ResourceBase resource, Func<ShareServiceClient, Task<ResourceResponse>> operation)
        => HandleRequest(resource, factory => operation(factory.CreateShareServiceClient()));

    protected Task<ResourceResponse> HandleQueueRequest(ResourceBase resource, Func<QueueServiceClient, Task<ResourceResponse>> operation)
        => HandleRequest(resource, factory => operation(factory.CreateQueueServiceClient()));

    protected Task<ResourceResponse> HandleTableRequest(ResourceBase resource, Func<TableServiceClient, Task<ResourceResponse>> operation)
        => HandleRequest(resource, factory => operation(factory.CreateTableServiceClient()));

    protected Task<ResourceResponse> HandleDataLakeRequest(ResourceBase resource, Func<DataLakeServiceClient, Task<ResourceResponse>> operation)
        => HandleRequest(resource, factory => operation(factory.CreateDataLakeServiceClient()));

    private async Task<ResourceResponse> HandleRequest(ResourceBase resource, Func<StorageClientFactory, Task<ResourceResponse>> operation)
    {
        try
        {
            return await operation(new StorageClientFactory(resource.Config));
        }
        catch (RequestFailedException exception)
        {
            throw new ResourceErrorException(
                exception.ErrorCode ?? "StorageApiError",
                exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            throw new ResourceErrorException("InvalidConfiguration", exception.Message);
        }
        catch (ResourceErrorException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new ResourceErrorException(
                "UnhandledException",
                exception.Message,
                details:
                [
                    new ErrorDetail
                    {
                        Code = exception.GetType().Name,
                        Message = exception.ToString(),
                    }
                ]);
        }
    }

    protected static IDictionary<string, string> ToMetadata(Dictionary<string, string>? entries)
        => entries is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(entries, StringComparer.OrdinalIgnoreCase);

    protected static Dictionary<string, string>? FromMetadata(IDictionary<string, string>? metadata)
        => metadata is { Count: > 0 }
            ? metadata
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
            : null;

    protected static byte[] ResolveContent(string? content, string? contentBase64, string target)
        => (content, contentBase64) switch
        {
            (not null, not null) => throw new ResourceErrorException(
                "ConflictingContent",
                "Only one of 'content' and 'contentBase64' may be set.",
                target),
            (not null, null) => System.Text.Encoding.UTF8.GetBytes(content),
            (null, not null) => DecodeBase64(contentBase64, target),
            _ => [],
        };

    protected static byte[] DecodeBase64(string value, string target)
    {
        try
        {
            return Convert.FromBase64String(value);
        }
        catch (FormatException)
        {
            throw new ResourceErrorException("InvalidBase64", "The value is not valid base64.", target);
        }
    }

    protected static Uri ParseAbsoluteUri(string value, string target)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? uri
            : throw new ResourceErrorException("InvalidUri", $"'{value}' is not a valid absolute URI.", target);

    protected static string ToIso8601(DateTimeOffset? value)
        => (value ?? default).ToString("O", CultureInfo.InvariantCulture);

    protected static DateTimeOffset ParseTimestamp(string value, string target)
        => DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var result)
            ? result
            : throw new ResourceErrorException("InvalidDateTime", $"'{value}' is not a valid ISO 8601 date and time.", target);

    protected static string? NullIfEmpty(string? value)
        => string.IsNullOrEmpty(value) ? null : value;

    /// <summary>
    /// Fails the deployment when a declared value differs from a property that the service only
    /// accepts at creation time, rather than silently leaving the resource out of sync.
    /// </summary>
    protected static void RejectImmutableChange(string resourceName, string property, object? declared, object? actual)
    {
        if (declared is null || declared.Equals(actual))
        {
            return;
        }

        var current = actual is null || (actual as string)?.Length == 0
            ? "it is currently unset"
            : $"it is currently '{actual}'";

        throw new ResourceErrorException(
            "ImmutablePropertyChanged",
            $"'{property}' can only be set when '{resourceName}' is created, and {current}.",
            property);
    }

    /// <summary>
    /// Normalizes a directory path within a share or file system: no leading or trailing slash,
    /// and backslashes folded to forward slashes.
    /// </summary>
    protected static string NormalizePath(string? path)
        => (path ?? string.Empty).Replace('\\', '/').Trim('/');
}
