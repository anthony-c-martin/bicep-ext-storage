using Azure;
using Azure.Core;
using Azure.Data.Tables;
using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Files.DataLake;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using Azure.Storage.Queues;

namespace Bicep.Extension.Storage.Handlers;

/// <summary>
/// Builds data plane service clients for the account described by the extension
/// <see cref="Configuration"/>, using whichever credential was supplied.
/// </summary>
internal sealed class StorageClientFactory
{
    private const string DefaultEndpointSuffix = "core.windows.net";

    private readonly Configuration configuration;

    public StorageClientFactory(Configuration configuration)
    {
        this.configuration = configuration;

        var credentials = new[]
        {
            configuration.AccountKey is not null,
            configuration.SasToken is not null,
            configuration.AccessToken is not null,
        }.Count(supplied => supplied);

        if (credentials > 1)
        {
            throw new InvalidOperationException(
                "Only one of 'accountKey', 'sasToken' or 'accessToken' may be set on the storage extension configuration.");
        }

        if (credentials == 1 && configuration.UseDefaultAzureCredential == true)
        {
            throw new InvalidOperationException(
                "'useDefaultAzureCredential' cannot be combined with 'accountKey', 'sasToken' or 'accessToken'.");
        }

        if (credentials == 0 && configuration.UseDefaultAzureCredential == false)
        {
            throw new InvalidOperationException(
                "No credential was supplied. Set 'accountKey', 'sasToken' or 'accessToken', or leave 'useDefaultAzureCredential' unset.");
        }

        if (string.IsNullOrWhiteSpace(configuration.AccountName))
        {
            throw new InvalidOperationException("'accountName' is required on the storage extension configuration.");
        }
    }

    public BlobServiceClient CreateBlobServiceClient()
    {
        var uri = ServiceUri(configuration.BlobEndpoint, "blob");

        if (SharedKey is { } sharedKey)
        {
            return new BlobServiceClient(uri, sharedKey);
        }

        if (Sas is { } sas)
        {
            return new BlobServiceClient(uri, sas);
        }

        return new BlobServiceClient(uri, TokenCredential);
    }

    public QueueServiceClient CreateQueueServiceClient()
    {
        var uri = ServiceUri(configuration.QueueEndpoint, "queue");

        if (SharedKey is { } sharedKey)
        {
            return new QueueServiceClient(uri, sharedKey);
        }

        if (Sas is { } sas)
        {
            return new QueueServiceClient(uri, sas);
        }

        return new QueueServiceClient(uri, TokenCredential);
    }

    public ShareServiceClient CreateShareServiceClient()
    {
        var uri = ServiceUri(configuration.FileEndpoint, "file");

        if (SharedKey is { } sharedKey)
        {
            return new ShareServiceClient(uri, sharedKey);
        }

        if (Sas is { } sas)
        {
            return new ShareServiceClient(uri, sas);
        }

        // The file service requires callers to declare their intent when using Microsoft Entra
        // authentication; without it every data plane call is rejected.
        return new ShareServiceClient(uri, TokenCredential, new ShareClientOptions { ShareTokenIntent = ShareTokenIntent.Backup });
    }

    public DataLakeServiceClient CreateDataLakeServiceClient()
    {
        var uri = ServiceUri(configuration.DfsEndpoint, "dfs");

        if (SharedKey is { } sharedKey)
        {
            return new DataLakeServiceClient(uri, sharedKey);
        }

        if (Sas is { } sas)
        {
            return new DataLakeServiceClient(uri, sas);
        }

        return new DataLakeServiceClient(uri, TokenCredential);
    }

    public TableServiceClient CreateTableServiceClient()
    {
        var uri = ServiceUri(configuration.TableEndpoint, "table");

        if (configuration.AccountKey is { } accountKey)
        {
            return new TableServiceClient(uri, new TableSharedKeyCredential(configuration.AccountName, accountKey));
        }

        if (Sas is { } sas)
        {
            return new TableServiceClient(uri, sas);
        }

        return new TableServiceClient(uri, TokenCredential);
    }

    private StorageSharedKeyCredential? SharedKey
        => configuration.AccountKey is { } accountKey
            ? new StorageSharedKeyCredential(configuration.AccountName, accountKey)
            : null;

    private AzureSasCredential? Sas
        => configuration.SasToken is { } sasToken
            ? new AzureSasCredential(sasToken.TrimStart('?'))
            : null;

    private TokenCredential TokenCredential
        => configuration.AccessToken is { } accessToken
            ? new StaticTokenCredential(accessToken)
            : new DefaultAzureCredential();

    private Uri ServiceUri(string? configured, string service)
    {
        if (configured is not null)
        {
            return Uri.TryCreate(configured, UriKind.Absolute, out var explicitUri)
                ? explicitUri
                : throw new InvalidOperationException($"'{configured}' is not a valid absolute {service} service endpoint URI.");
        }

        var suffix = (configuration.EndpointSuffix ?? DefaultEndpointSuffix).Trim('.');

        return new Uri($"https://{configuration.AccountName}.{service}.{suffix}");
    }

    /// <summary>
    /// Presents a caller-supplied access token to the SDK. The token is used verbatim, so its
    /// lifetime is bounded by the deployment.
    /// </summary>
    private sealed class StaticTokenCredential(string accessToken) : TokenCredential
    {
        // The SDK only refreshes on expiry; report a horizon well past any single deployment so
        // the token is never treated as stale mid-deployment.
        private readonly AccessToken token = new(accessToken, DateTimeOffset.UtcNow.AddHours(24));

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => token;

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => new(token);
    }
}
