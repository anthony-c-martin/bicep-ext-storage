using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using FileShare = Bicep.Extension.Storage.Models.FileShare;

namespace Bicep.Extension.Storage.Handlers;

public sealed class FileShareHandler : StorageResourceHandlerBase<FileShare, FileShareIdentifiers>
{
    protected override Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
        => Task.FromResult(GetResponse(request));

    protected override Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
        => HandleShareRequest(request, async service =>
        {
            var declared = request.Properties;
            var share = service.GetShareClient(declared.Name);
            var metadata = ToMetadata(declared.Metadata);

            var created = await share.CreateIfNotExistsAsync(
                new ShareCreateOptions
                {
                    Metadata = metadata,
                    QuotaInGB = declared.Quota,
                    AccessTier = ToAccessTier(declared.AccessTier),
                    Protocols = ToProtocols(declared.EnabledProtocol),
                },
                cancellationToken);

            // The SDK returns a null response - not a null value - when the share already exists.
            // The enabled protocol is fixed at creation time, so only the mutable settings are
            // reconciled here.
            if (created is null)
            {
                await share.SetMetadataAsync(metadata, cancellationToken: cancellationToken);

                if (declared.Quota is not null || declared.AccessTier is not null)
                {
                    await share.SetPropertiesAsync(
                        new ShareSetPropertiesOptions
                        {
                            QuotaInGB = declared.Quota,
                            AccessTier = ToAccessTier(declared.AccessTier),
                        },
                        cancellationToken);
                }
            }

            var result = await ReadAsync(share, cancellationToken);

            if (created is null)
            {
                RejectImmutableChange(declared.Name, "enabledProtocol", declared.EnabledProtocol, result.EnabledProtocol ?? FileShareProtocol.Smb);
            }

            // Echo the declared state so the response matches what Bicep asked for, taking only the
            // service-assigned read-only fields from the read.
            declared.Url = result.Url;
            declared.ETag = result.ETag;
            declared.LastModified = result.LastModified;

            return BuildResponse(request.Type, request.ApiVersion, declared);
        });

    protected override Task<ResourceResponse> Get(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleShareRequest(request, async service =>
            BuildResponse(
                request.Type,
                request.ApiVersion,
                await ReadAsync(service.GetShareClient(request.Identifiers.Name), cancellationToken)));

    protected override Task<ResourceResponse> Delete(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleShareRequest(request, async service =>
        {
            await service.GetShareClient(request.Identifiers.Name).DeleteIfExistsAsync(cancellationToken: cancellationToken);

            return GetResponse(request, properties: null);
        });

    protected override FileShareIdentifiers GetIdentifiers(FileShare properties)
        => new() { Name = properties.Name };

    private static async Task<FileShare> ReadAsync(ShareClient share, CancellationToken cancellationToken)
    {
        var properties = (await share.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;

        return new FileShare
        {
            Name = share.Name,
            Quota = properties.QuotaInGB,
            AccessTier = FromAccessTier(properties.AccessTier),
            EnabledProtocol = FromProtocols(properties.Protocols),
            Metadata = FromMetadata(properties.Metadata),
            Url = share.Uri.ToString(),
            ETag = properties.ETag.ToString(),
            LastModified = ToIso8601(properties.LastModified),
        };
    }

    private static ResourceResponse BuildResponse(string type, string? apiVersion, FileShare properties)
        => new()
        {
            Type = type,
            ApiVersion = apiVersion,
            Properties = properties,
            Identifiers = new FileShareIdentifiers { Name = properties.Name },
        };

    private static ShareAccessTier? ToAccessTier(FileShareAccessTier? accessTier)
        => accessTier switch
        {
            FileShareAccessTier.TransactionOptimized => ShareAccessTier.TransactionOptimized,
            FileShareAccessTier.Hot => ShareAccessTier.Hot,
            FileShareAccessTier.Cool => ShareAccessTier.Cool,
            FileShareAccessTier.Premium => ShareAccessTier.Premium,
            _ => null,
        };

    private static FileShareAccessTier? FromAccessTier(string? accessTier)
        => accessTier switch
        {
            "TransactionOptimized" => FileShareAccessTier.TransactionOptimized,
            "Hot" => FileShareAccessTier.Hot,
            "Cool" => FileShareAccessTier.Cool,
            "Premium" => FileShareAccessTier.Premium,
            _ => null,
        };

    private static ShareProtocols? ToProtocols(FileShareProtocol? protocol)
        => protocol switch
        {
            FileShareProtocol.Smb => ShareProtocols.Smb,
            FileShareProtocol.Nfs => ShareProtocols.Nfs,
            _ => null,
        };

    private static FileShareProtocol? FromProtocols(ShareProtocols? protocols)
        => protocols switch
        {
            ShareProtocols.Nfs => FileShareProtocol.Nfs,
            ShareProtocols.Smb => FileShareProtocol.Smb,
            _ => null,
        };
}
