using Bicep.Extension.Storage.Handlers;

namespace Bicep.Extension.Storage.Tests;

/// <summary>
/// Validation errors are raised before any network call, so they can be exercised end to end.
/// </summary>
[TestClass]
public class ValidationHandlerTests
{
    [TestMethod]
    public async Task Blob_RejectsContentCombinedWithSourceUri()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new BlobHandler(),
            "Blob",
            new
            {
                containerName = "artifacts",
                name = "copied.txt",
                content = "hello",
                sourceUri = "https://example.blob.core.windows.net/src/file.txt",
            });

        Assert.AreEqual("ConflictingContent", response.ErrorCode());
    }

    [TestMethod]
    public async Task Blob_RejectsPageBlobWithoutSize()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new BlobHandler(),
            "Blob",
            new { containerName = "disks", name = "os.vhd", type = "Page" });

        Assert.AreEqual("MissingSize", response.ErrorCode());
    }

    [TestMethod]
    public async Task Blob_RejectsInvalidBase64Content()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new BlobHandler(),
            "Blob",
            new { containerName = "artifacts", name = "bad.bin", contentBase64 = "not base64!" });

        Assert.AreEqual("InvalidBase64", response.ErrorCode());
    }

    [TestMethod]
    public async Task Blob_RejectsBothContentForms()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new BlobHandler(),
            "Blob",
            new { containerName = "artifacts", name = "dup.txt", content = "a", contentBase64 = "YQ==" });

        Assert.AreEqual("ConflictingContent", response.ErrorCode());
    }

    [TestMethod]
    public async Task DataLakeDirectory_RejectsPermissionsCombinedWithAccessControl()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new DataLakeDirectoryHandler(),
            "DataLakeDirectory",
            new
            {
                fileSystemName = "raw",
                path = "landing",
                permissions = "0750",
                accessControl = new[] { new { type = "Other", permissions = "r-x" } },
            });

        Assert.AreEqual("ConflictingAccessControl", response.ErrorCode());
    }

    [TestMethod]
    public async Task DataLakeDirectory_RejectsEmptyPath()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new DataLakeDirectoryHandler(),
            "DataLakeDirectory",
            new { fileSystemName = "raw", path = "/" });

        Assert.AreEqual("InvalidPath", response.ErrorCode());
    }

    [TestMethod]
    public async Task DataLakeDirectory_RejectsEmptyAccessControl()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new DataLakeDirectoryHandler(),
            "DataLakeDirectory",
            new { fileSystemName = "raw", path = "landing", accessControl = Array.Empty<object>() });

        Assert.AreEqual("EmptyAccessControl", response.ErrorCode());
    }

    [TestMethod]
    public async Task DataLakeFileSystem_RejectsEmptyAccessControl()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new DataLakeFileSystemHandler(),
            "DataLakeFileSystem",
            new { name = "raw", accessControl = Array.Empty<object>() });

        Assert.AreEqual("EmptyAccessControl", response.ErrorCode());
    }

    [TestMethod]
    public async Task TableEntity_RejectsReservedProperties()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new TableEntityHandler(),
            "TableEntity",
            new
            {
                tableName = "settings",
                partitionKey = "prod",
                rowKey = "limits",
                entity = new Dictionary<string, string> { ["RowKey"] = "override" },
            });

        Assert.AreEqual("ReservedProperty", response.ErrorCode());
    }

    [TestMethod]
    public async Task TableEntity_RejectsValuesThatDoNotMatchTheirDeclaredType()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new TableEntityHandler(),
            "TableEntity",
            new
            {
                tableName = "settings",
                partitionKey = "prod",
                rowKey = "limits",
                entity = new { maxItems = "lots" },
                entityTypes = new { maxItems = "Int32" },
            });

        Assert.AreEqual("InvalidEntityValue", response.ErrorCode());
    }

    [TestMethod]
    public async Task Configuration_RejectsMultipleCredentials()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new QueueHandler(),
            "Queue",
            new { name = "orders" },
            config: new { accountName = "teststorage", accountKey = "dGVzdA==", sasToken = "sv=2024-01-01" });

        Assert.AreEqual("InvalidConfiguration", response.ErrorCode());
        StringAssert.Contains(response.ErrorMessage(), "Only one of");
    }

    [TestMethod]
    public async Task Configuration_RejectsMissingCredentialWhenDefaultsAreDisabled()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new QueueHandler(),
            "Queue",
            new { name = "orders" },
            config: new { accountName = "teststorage", useDefaultAzureCredential = false });

        Assert.AreEqual("InvalidConfiguration", response.ErrorCode());
        StringAssert.Contains(response.ErrorMessage(), "No credential was supplied");
    }

    [TestMethod]
    public async Task Configuration_RejectsInvalidEndpointOverride()
    {
        var response = await HandlerHarness.CreateOrUpdateAsync(
            new QueueHandler(),
            "Queue",
            new { name = "orders" },
            config: new { accountName = "teststorage", accountKey = "dGVzdA==", queueEndpoint = "not-a-uri" });

        Assert.AreEqual("InvalidConfiguration", response.ErrorCode());
        StringAssert.Contains(response.ErrorMessage(), "queue service endpoint");
    }
}
