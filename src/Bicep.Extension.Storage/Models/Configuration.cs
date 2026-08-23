using Azure.Bicep.Types.Concrete;
using Bicep.Local.Extension.Types.Attributes;

namespace Bicep.Extension.Storage.Models;

/// <summary>
/// Extension configuration. The Azure Storage data plane is inherently account-scoped, so the
/// account and its credential are supplied once per extension declaration rather than repeated
/// on every resource.
/// </summary>
public class Configuration
{
    [TypeProperty("The storage account name", ObjectTypePropertyFlags.Required)]
    public required string AccountName { get; set; }

    [TypeProperty("The storage endpoint suffix. Defaults to 'core.windows.net'.")]
    public string? EndpointSuffix { get; set; }

    [TypeProperty("The blob service endpoint. Defaults to 'https://<accountName>.blob.<endpointSuffix>'. Set this to target an emulator or a custom domain.")]
    public string? BlobEndpoint { get; set; }

    [TypeProperty("The file service endpoint. Defaults to 'https://<accountName>.file.<endpointSuffix>'.")]
    public string? FileEndpoint { get; set; }

    [TypeProperty("The queue service endpoint. Defaults to 'https://<accountName>.queue.<endpointSuffix>'.")]
    public string? QueueEndpoint { get; set; }

    [TypeProperty("The table service endpoint. Defaults to 'https://<accountName>.table.<endpointSuffix>'.")]
    public string? TableEndpoint { get; set; }

    [TypeProperty("The Data Lake Storage Gen2 (DFS) endpoint. Defaults to 'https://<accountName>.dfs.<endpointSuffix>'.")]
    public string? DfsEndpoint { get; set; }

    [TypeProperty("A storage account shared key. Required for operations that do not support Microsoft Entra authentication, such as table stored access policies.", isSecure: true)]
    public string? AccountKey { get; set; }

    [TypeProperty("A shared access signature token, with or without a leading '?'.", isSecure: true)]
    public string? SasToken { get; set; }

    [TypeProperty("A Microsoft Entra access token for 'https://storage.azure.com'.", isSecure: true)]
    public string? AccessToken { get; set; }

    [TypeProperty("Whether to authenticate with DefaultAzureCredential (environment, workload identity, managed identity, Azure CLI, ...). Defaults to true when no other credential is supplied.")]
    public bool? UseDefaultAzureCredential { get; set; }
}
