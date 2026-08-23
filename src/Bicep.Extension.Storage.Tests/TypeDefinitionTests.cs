using System.Text.Json;
using Bicep.Extension.Storage.Models;
using Bicep.Local.Extension.Builder.Models;
using Bicep.Local.Extension.Types;
using Bicep.Local.Extension.Types.Models;

namespace Bicep.Extension.Storage.Tests;

/// <summary>
/// The Bicep type definition is built by reflection at startup, so unsupported property types only
/// surface at runtime. Generating it here keeps that failure at build time instead.
/// </summary>
[TestClass]
public class TypeDefinitionTests
{
    private static readonly string[] ExpectedResourceTypes =
    [
        "Blob",
        "BlobContainer",
        "DataLakeDirectory",
        "DataLakeFileSystem",
        "FileShare",
        "FileShareDirectory",
        "FileShareFile",
        "Queue",
        "Table",
        "TableEntity",
    ];

    private static TypeDefinition GenerateTypeDefinition()
        => new TypeDefinitionBuilder(
            new ExtensionInfo("storage", "0.0.1-test", isSingleton: true),
            new TypeProvider(
                [typeof(Configuration).Assembly],
                new ConfigurationTypeContainer(typeof(Configuration))))
            .GenerateTypeDefinition();

    [TestMethod]
    public void TypeDefinition_ExposesEveryResourceType()
    {
        var index = JsonSerializer.Deserialize<JsonElement>(GenerateTypeDefinition().IndexFileContent);

        var resourceTypes = index.GetProperty("resources").EnumerateObject().Select(x => x.Name).Order().ToArray();

        CollectionAssert.AreEqual(ExpectedResourceTypes, resourceTypes);
    }

    [TestMethod]
    public void TypeDefinition_MarksTheConfigurationSecretsAsSecure()
    {
        var types = JsonSerializer.Deserialize<JsonElement>(GenerateTypeDefinition().TypeFileContents["types.json"]);

        var configuration = types.EnumerateArray()
            .Single(type => type.TryGetProperty("$type", out var kind)
                && kind.GetString() == "ObjectType"
                && type.GetProperty("name").GetString() == nameof(Configuration));

        var properties = configuration.GetProperty("properties");

        foreach (var secret in new[] { "accountKey", "sasToken", "accessToken" })
        {
            var reference = properties.GetProperty(secret).GetProperty("type").GetProperty("$ref").GetString();
            var referenced = types[int.Parse(reference!.Split('/')[^1])];

            Assert.IsTrue(
                referenced.TryGetProperty("sensitive", out var sensitive) && sensitive.GetBoolean(),
                $"'{secret}' should be declared as a secure string.");
        }
    }

    [TestMethod]
    public void TypeDefinition_UsesDeclaredEnumCasingForLiterals()
    {
        var types = JsonSerializer.Deserialize<JsonElement>(GenerateTypeDefinition().TypeFileContents["types.json"]);

        var literals = types.EnumerateArray()
            .Where(type => type.GetProperty("$type").GetString() == "StringLiteralType")
            .Select(type => type.GetProperty("value").GetString())
            .ToHashSet(StringComparer.Ordinal);

        // Enum members are serialized verbatim, so the generated literals must match the C# names.
        CollectionAssert.IsSubsetOf(
            new[] { "TransactionOptimized", "Block", "Append", "Page", "Archive", "Default", "Int32" },
            literals.ToArray());
    }

    [TestMethod]
    public void TypeDefinition_ModelsMetadataAsAnObjectWithStringValues()
    {
        var types = JsonSerializer.Deserialize<JsonElement>(GenerateTypeDefinition().TypeFileContents["types.json"]);

        var queue = types.EnumerateArray()
            .Single(type => type.TryGetProperty("$type", out var kind)
                && kind.GetString() == "ObjectType"
                && type.GetProperty("name").GetString() == nameof(Queue));

        var metadata = queue.GetProperty("properties").GetProperty("metadata");
        var reference = metadata.GetProperty("type").GetProperty("$ref").GetString();
        var referenced = types[int.Parse(reference!.Split('/')[^1])];

        Assert.AreEqual("ObjectType", referenced.GetProperty("$type").GetString());
        Assert.IsTrue(referenced.TryGetProperty("additionalProperties", out _), "Metadata should accept arbitrary keys.");
    }
}
