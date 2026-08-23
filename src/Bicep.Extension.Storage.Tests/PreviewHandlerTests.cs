using System.Text.Json;
using Bicep.Extension.Storage.Handlers;

namespace Bicep.Extension.Storage.Tests;

/// <summary>
/// Preview never contacts the storage service, so it is the natural place to verify that the
/// declared Bicep shape round-trips through the handler unchanged.
/// </summary>
[TestClass]
public class PreviewHandlerTests
{
    [TestMethod]
    public async Task Preview_BlobContainer_EchoesDeclaredState()
    {
        var response = await HandlerHarness.PreviewAsync(
            new BlobContainerHandler(),
            "BlobContainer",
            new
            {
                name = "artifacts",
                publicAccess = "Blob",
                metadata = new { env = "prod", owner = "platform" },
            });

        var properties = response.ResourceProperties();

        Assert.AreEqual("artifacts", properties.GetProperty("name").GetString());
        Assert.AreEqual("Blob", properties.GetProperty("publicAccess").GetString());
        Assert.AreEqual("prod", properties.GetProperty("metadata").GetProperty("env").GetString());
        Assert.AreEqual("artifacts", response.ResourceIdentifiers().GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task Preview_Blob_PreservesContentAndTags()
    {
        var response = await HandlerHarness.PreviewAsync(
            new BlobHandler(),
            "Blob",
            new
            {
                containerName = "artifacts",
                name = "config/app.json",
                content = "{}",
                contentType = "application/json",
                accessTier = "Cool",
                tags = new { project = "bicep" },
            });

        var properties = response.ResourceProperties();

        Assert.AreEqual("{}", properties.GetProperty("content").GetString());
        Assert.AreEqual("application/json", properties.GetProperty("contentType").GetString());
        Assert.AreEqual("Cool", properties.GetProperty("accessTier").GetString());
        Assert.AreEqual("bicep", properties.GetProperty("tags").GetProperty("project").GetString());

        var identifiers = response.ResourceIdentifiers();
        Assert.AreEqual("artifacts", identifiers.GetProperty("containerName").GetString());
        Assert.AreEqual("config/app.json", identifiers.GetProperty("name").GetString());
    }

    [TestMethod]
    public async Task Preview_FileShare_KeepsEnumCasing()
    {
        var response = await HandlerHarness.PreviewAsync(
            new FileShareHandler(),
            "FileShare",
            new { name = "profiles", quota = 128, accessTier = "TransactionOptimized", enabledProtocol = "Smb" });

        var properties = response.ResourceProperties();

        Assert.AreEqual(128, properties.GetProperty("quota").GetInt32());
        Assert.AreEqual("TransactionOptimized", properties.GetProperty("accessTier").GetString());
        Assert.AreEqual("Smb", properties.GetProperty("enabledProtocol").GetString());
    }

    [TestMethod]
    public async Task Preview_FileShareDirectory_NormalizesPathInIdentifiers()
    {
        var response = await HandlerHarness.PreviewAsync(
            new FileShareDirectoryHandler(),
            "FileShareDirectory",
            new { shareName = "profiles", path = "/teams/platform/" });

        // The normalized path must appear in both places, so that preview and deployment agree.
        Assert.AreEqual("teams/platform", response.ResourceIdentifiers().GetProperty("path").GetString());
        Assert.AreEqual("teams/platform", response.ResourceProperties().GetProperty("path").GetString());
    }

    [TestMethod]
    public async Task Preview_DataLakeDirectory_NormalizesBackslashSeparators()
    {
        var response = await HandlerHarness.PreviewAsync(
            new DataLakeDirectoryHandler(),
            "DataLakeDirectory",
            new { fileSystemName = "raw", path = @"landing\events\" });

        Assert.AreEqual("landing/events", response.ResourceIdentifiers().GetProperty("path").GetString());
        Assert.AreEqual("landing/events", response.ResourceProperties().GetProperty("path").GetString());
    }

    [TestMethod]
    public async Task Preview_TableEntity_PreservesTypeOverrides()
    {
        var response = await HandlerHarness.PreviewAsync(
            new TableEntityHandler(),
            "TableEntity",
            new
            {
                tableName = "settings",
                partitionKey = "prod",
                rowKey = "limits",
                entity = new { maxItems = "50", name = "prod" },
                entityTypes = new { maxItems = "Int32" },
            });

        var properties = response.ResourceProperties();

        Assert.AreEqual("50", properties.GetProperty("entity").GetProperty("maxItems").GetString());
        Assert.AreEqual("Int32", properties.GetProperty("entityTypes").GetProperty("maxItems").GetString());

        var identifiers = response.ResourceIdentifiers();
        Assert.AreEqual("settings", identifiers.GetProperty("tableName").GetString());
        Assert.AreEqual("prod", identifiers.GetProperty("partitionKey").GetString());
        Assert.AreEqual("limits", identifiers.GetProperty("rowKey").GetString());
    }

    [TestMethod]
    public async Task Preview_DataLakeFileSystem_RoundTripsAccessControl()
    {
        var response = await HandlerHarness.PreviewAsync(
            new DataLakeFileSystemHandler(),
            "DataLakeFileSystem",
            new
            {
                name = "raw",
                accessControl = new[]
                {
                    new { scope = "Default", type = "Group", id = "8b0a1e9c-9b6e-4a6c-9a1b-0f4b1a9c2d3e", permissions = "r-x" },
                },
            });

        var accessControl = response.ResourceProperties().GetProperty("accessControl")[0];

        Assert.AreEqual("Default", accessControl.GetProperty("scope").GetString());
        Assert.AreEqual("Group", accessControl.GetProperty("type").GetString());
        Assert.AreEqual("r-x", accessControl.GetProperty("permissions").GetString());
    }

    [TestMethod]
    public async Task Preview_OmitsUnsetOptionalProperties()
    {
        var response = await HandlerHarness.PreviewAsync(
            new QueueHandler(),
            "Queue",
            new { name = "orders" });

        var properties = response.ResourceProperties();

        Assert.AreEqual("orders", properties.GetProperty("name").GetString());
        Assert.AreEqual(JsonValueKind.Undefined, properties.TryGetProperty("metadata", out var metadata) ? metadata.ValueKind : JsonValueKind.Undefined);
        Assert.AreEqual(JsonValueKind.Undefined, properties.TryGetProperty("url", out var url) ? url.ValueKind : JsonValueKind.Undefined);
    }
}
