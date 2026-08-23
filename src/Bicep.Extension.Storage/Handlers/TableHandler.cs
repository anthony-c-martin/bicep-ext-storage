using Azure.Data.Tables;
using Azure.Data.Tables.Models;
using TableAccessPolicy = Bicep.Extension.Storage.Models.TableAccessPolicy;

namespace Bicep.Extension.Storage.Handlers;

public sealed class TableHandler : StorageResourceHandlerBase<Table, TableIdentifiers>
{
    protected override Task<ResourceResponse> Preview(ResourceRequest request, CancellationToken cancellationToken)
        => Task.FromResult(GetResponse(request));

    protected override Task<ResourceResponse> CreateOrUpdate(ResourceRequest request, CancellationToken cancellationToken)
        => HandleTableRequest(request, async service =>
        {
            var declared = request.Properties;
            var table = service.GetTableClient(declared.Name);

            await table.CreateIfNotExistsAsync(cancellationToken);

            if (declared.AccessPolicies is { } accessPolicies)
            {
                await table.SetAccessPolicyAsync(
                    accessPolicies.Select(ToSignedIdentifier).ToList(),
                    cancellationToken);
            }

            // The policies were just written, so echo the declared values rather than the
            // service's timestamp formatting, which would otherwise show up as a spurious diff.
            return BuildResponse(
                request.Type,
                request.ApiVersion,
                new Table
                {
                    Name = table.Name,
                    AccessPolicies = declared.AccessPolicies,
                    Url = table.Uri.ToString(),
                });
        });

    protected override Task<ResourceResponse> Get(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleTableRequest(request, async service =>
            BuildResponse(
                request.Type,
                request.ApiVersion,
                await ReadAsync(
                    service.GetTableClient(request.Identifiers.Name),
                    // The table ACL API only accepts shared key authentication, so reading policies
                    // under any other credential would fail the whole reference.
                    readAccessPolicies: request.Config.AccountKey is not null,
                    cancellationToken)));

    protected override Task<ResourceResponse> Delete(ReferenceRequest request, CancellationToken cancellationToken)
        => HandleTableRequest(request, async service =>
        {
            await service.DeleteTableAsync(request.Identifiers.Name, cancellationToken);

            return GetResponse(request, properties: null);
        });

    protected override TableIdentifiers GetIdentifiers(Table properties)
        => new() { Name = properties.Name };

    private static async Task<Table> ReadAsync(TableClient table, bool readAccessPolicies, CancellationToken cancellationToken)
    {
        var accessPolicies = readAccessPolicies
            ? (await table.GetAccessPoliciesAsync(cancellationToken)).Value
            : null;

        return new Table
        {
            Name = table.Name,
            AccessPolicies = accessPolicies is { Count: > 0 }
                ? accessPolicies.Select(FromSignedIdentifier).ToArray()
                : null,
            Url = table.Uri.ToString(),
        };
    }

    private static ResourceResponse BuildResponse(string type, string? apiVersion, Table properties)
        => new()
        {
            Type = type,
            ApiVersion = apiVersion,
            Properties = properties,
            Identifiers = new TableIdentifiers { Name = properties.Name },
        };

    private static TableSignedIdentifier ToSignedIdentifier(TableAccessPolicy policy)
        => new(
            policy.Id,
            new Azure.Data.Tables.Models.TableAccessPolicy(
                policy.Start is null ? null : ParseTimestamp(policy.Start, "accessPolicies.start"),
                policy.Expiry is null ? null : ParseTimestamp(policy.Expiry, "accessPolicies.expiry"),
                policy.Permissions));

    private static TableAccessPolicy FromSignedIdentifier(TableSignedIdentifier identifier)
        => new()
        {
            Id = identifier.Id,
            Permissions = identifier.AccessPolicy.Permission ?? string.Empty,
            Start = identifier.AccessPolicy.StartsOn is { } start ? ToIso8601(start) : null,
            Expiry = identifier.AccessPolicy.ExpiresOn is { } expiry ? ToIso8601(expiry) : null,
        };
}
