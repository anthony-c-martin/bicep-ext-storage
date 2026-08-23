using System.Text.Json;
using Bicep.Local.Extension.Host.Handlers;
using Bicep.Local.Rpc;

namespace Bicep.Extension.Storage.Tests;

/// <summary>
/// Helpers for invoking resource handlers through their public <see cref="IResourceHandler"/> entry
/// point, mirroring how the Bicep local-deploy host calls them (JSON in, JSON out).
/// </summary>
public static class HandlerHarness
{
    // The Bicep host exchanges properties/config as camelCase JSON.
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public static object DefaultConfig { get; } = new { accountName = "teststorage", accountKey = "dGVzdC1rZXk=" };

    public static Task<LocalExtensibilityOperationResponse> PreviewAsync(
        IResourceHandler handler,
        string type,
        object properties,
        object? config = null,
        CancellationToken cancellationToken = default)
        => handler.Preview(Specification(type, properties, config), cancellationToken);

    public static Task<LocalExtensibilityOperationResponse> CreateOrUpdateAsync(
        IResourceHandler handler,
        string type,
        object properties,
        object? config = null,
        CancellationToken cancellationToken = default)
        => handler.CreateOrUpdate(Specification(type, properties, config), cancellationToken);

    public static JsonElement ResourceProperties(this LocalExtensibilityOperationResponse response)
    {
        Assert.IsNull(response.ErrorData, response.ErrorData?.Error?.Message);
        Assert.IsNotNull(response.Resource);
        return JsonSerializer.Deserialize<JsonElement>(response.Resource.Properties);
    }

    public static JsonElement ResourceIdentifiers(this LocalExtensibilityOperationResponse response)
    {
        Assert.IsNull(response.ErrorData, response.ErrorData?.Error?.Message);
        Assert.IsNotNull(response.Resource);
        return JsonSerializer.Deserialize<JsonElement>(response.Resource.Identifiers);
    }

    public static string ErrorCode(this LocalExtensibilityOperationResponse response)
    {
        Assert.IsNotNull(response.ErrorData, "Expected the operation to fail.");
        return response.ErrorData.Error.Code;
    }

    public static string ErrorMessage(this LocalExtensibilityOperationResponse response)
    {
        Assert.IsNotNull(response.ErrorData, "Expected the operation to fail.");
        return response.ErrorData.Error.Message;
    }

    private static ResourceSpecification Specification(string type, object properties, object? config)
        => new()
        {
            Type = type,
            Config = JsonSerializer.Serialize(config ?? DefaultConfig, SerializerOptions),
            Properties = JsonSerializer.Serialize(properties, SerializerOptions),
        };
}
