# Azure Storage Bicep Extension

This experimental local-deploy extension manages **Azure Storage data plane** resources with Bicep —
the things that live *inside* a storage account (containers, blobs, shares, queues, tables and Data
Lake paths), rather than the account itself. Provision the account with ARM/Bicep as usual, then use
this extension to populate it.

It is built on the official Azure Storage .NET SDKs: [`Azure.Storage.Blobs`](https://www.nuget.org/packages/Azure.Storage.Blobs),
[`Azure.Storage.Files.Shares`](https://www.nuget.org/packages/Azure.Storage.Files.Shares),
[`Azure.Storage.Files.DataLake`](https://www.nuget.org/packages/Azure.Storage.Files.DataLake),
[`Azure.Storage.Queues`](https://www.nuget.org/packages/Azure.Storage.Queues) and
[`Azure.Data.Tables`](https://www.nuget.org/packages/Azure.Data.Tables).

## Supported resources

The extension covers the resources that make up the contents of a storage account, across all five
data plane services:

| Resource type | Service | Manages |
| --- | --- | --- |
| `BlobContainer` | Blob | A container, its public access level and metadata |
| `Blob` | Blob | A block, page or append blob, with content, headers, tier and index tags |
| `FileShare` | Files | A file share, its quota, access tier and protocol |
| `Queue` | Queue | A queue and its metadata |
| `DataLakeFileSystem` | Data Lake (DFS) | A file system and the access control on its root |
| `Table` | Table | A table and its stored access policies |
| `DataLakeDirectory` | Data Lake (DFS) | A directory, its POSIX permissions and access control |
| `TableEntity` | Table | A single entity, with typed property values |
| `FileShareDirectory` | Files | A directory within a share |
| `FileShareFile` | Files | A file within a share, with content and headers |

Account-level settings — lifecycle management, encryption scopes, inventory policies, object
replication, static website configuration, SFTP local users and container immutability policies —
are control plane concerns and belong in `Microsoft.Storage/storageAccounts` resources, not here.

All Bicep properties use camelCase. Every resource exposes read-only outputs such as `url`, `eTag`
and `lastModified`; see the models in [`Models`](./src/Bicep.Extension.Storage/Models) for the full
property set.

### Blob content

`Blob` and `FileShareFile` accept content in two mutually exclusive forms:

- `content` for inline UTF-8 text — pair it with Bicep's `string()` function to write JSON.
- `contentBase64` for binary or on-disk content — pair it with Bicep's `loadFileAsBase64()`.

`Blob` additionally supports `sourceUri` to copy from another blob or a public URL. Block, append and
page blobs are all supported; page blobs require `sizeInMb`.

Blob writes are declarative rather than incremental: each deployment replaces the blob with the
declared content, so re-running a deployment does not append to an existing append blob.

### Table entities

`TableEntity.entity` is a map of string values. Because the Azure Table service is typed,
`entityTypes` lets you opt individual properties into a richer OData EDM type — `Int32`, `Int64`,
`Double`, `Boolean`, `DateTime`, `Guid` or `Binary`:

```bicep
resource limits 'TableEntity' = {
  tableName: settings.name
  partitionKey: 'orders'
  rowKey: 'limits'
  entity: {
    maxItems: '50'
    region: 'westeurope'
  }
  entityTypes: {
    maxItems: 'Int32'
  }
}
```

### Data Lake access control

`DataLakeFileSystem` and `DataLakeDirectory` accept POSIX `accessControl` entries, plus `owner` and
`group`. `DataLakeDirectory` also accepts octal `permissions`, which cannot be combined with
`accessControl`. Access control requires a hierarchical namespace-enabled account.

## Authentication

The Azure Storage data plane is account-scoped, so the account and its credential are configured
once per `extension` declaration:

```bicep
extension storage with {
  accountName: 'contosodata'
}
```

With no credential set, the extension authenticates using `DefaultAzureCredential` — which picks up
`az login`, environment variables, workload identity and managed identity. To use an explicit
credential, set exactly one of:

| Property | Use for |
| --- | --- |
| `accountKey` | Shared key. Required for table stored access policies, which reject Entra tokens. |
| `sasToken` | A shared access signature, with or without a leading `?`. |
| `accessToken` | A Microsoft Entra access token for `https://storage.azure.com`. |

To target a sovereign cloud, an emulator, or a custom domain, set `endpointSuffix` (default
`core.windows.net`) or override an individual service endpoint with `blobEndpoint`, `fileEndpoint`,
`queueEndpoint`, `tableEndpoint` or `dfsEndpoint`.

Managing several accounts in one deployment means declaring the extension more than once:

```bicep
extension storage with { accountName: 'contososource' } as source
extension storage with { accountName: 'contosotarget' } as target
```

## Build and test

```sh
dotnet build .
dotnet test
./scripts/publish.ps1 ./bicep-ext-storage
```

The MSTest project runs with the Microsoft Testing Platform runner. Handler tests invoke the public
`IResourceHandler` entry points through a JSON-based test harness, keeping serialization, validation
and camelCase behavior covered without contacting Azure. `TypeDefinitionTests` generates the Bicep
type definition so that unsupported property types fail the build rather than at runtime. Run one
class with:

```sh
dotnet test --filter "FullyQualifiedName~TypeDefinitionTests"
```

See the [Bicep extension unit testing guide](https://github.com/Azure/bicep/blob/main/docs/experimental/local-deploy-dotnet-unittesting-guide.md)
for the recommended handler testing approach.

[`samples/bicepconfig.json`](./samples/bicepconfig.json) points at the locally published build:

```json
{
  "experimentalFeaturesEnabled": {
    "localDeploy": true
  },
  "extensions": {
    "storage": "../bicep-ext-storage"
  },
  "implicitExtensions": []
}
```

Once a version has been released, swap that for
`"storage": "br:ghcr.io/anthony-c-martin/bicep-ext-storage:<version>"`.

### Testing against Azurite

Blob, queue and table resources can be exercised end to end without an Azure subscription by
pointing the extension at [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite):

```sh
npm install -g azurite
azurite --skipApiVersionCheck --location /tmp/azurite
```

```bicep
extension storage with {
  accountName: 'devstoreaccount1'
  accountKey: azuriteKey
  blobEndpoint: 'http://127.0.0.1:10000/devstoreaccount1'
  queueEndpoint: 'http://127.0.0.1:10001/devstoreaccount1'
  tableEndpoint: 'http://127.0.0.1:10002/devstoreaccount1'
}
```

Azurite does not emulate the Files or Data Lake endpoints.

## Samples

- [`basic`](./samples/basic) creates a blob container and writes a JSON settings blob.
- [`static-content`](./samples/static-content) publishes a file from disk to a public container, and shows append and cool-tier blobs.
- [`file-share`](./samples/file-share) creates a file share with a nested directory structure and a config file.
- [`messaging`](./samples/messaging) creates queues, a table with a stored access policy, and typed seed entities.
- [`data-lake`](./samples/data-lake) creates a medallion Data Lake layout with POSIX access control.

Deploy any sample parameter file:

```sh
az login
export STORAGE_ACCOUNT=<your storage account name>
bicep local-deploy ./samples/basic/main.bicepparam
```

This command creates or updates real Azure Storage resources, and overwrites blobs and files that
share a name with the declared ones. Review the sample before deploying it.

Set `BICEP_TRACING_ENABLED=true` to enable verbose local-deploy tracing.

## Releasing

Releases are cut manually so that versioning stays under explicit control — pushing to `main` does
not publish anything. To release, run the **Release** workflow from the Actions tab (or with
`gh workflow run release.yml -f version=0.2.0`) and supply the exact version to publish.

The workflow validates the version, builds and tests, publishes
`br:ghcr.io/anthony-c-martin/bicep-ext-storage:<version>` and then pushes a `v`-prefixed git tag
(`v0.2.0`) and GitHub Release. Note that the OCI artifact is tagged with the bare version, while the
git tag carries the `v` prefix. The workflow refuses to run if the tag already exists, so published
versions are never replaced; releases must be cut from `main`.

The version supplied to the workflow is stamped into the binary via `-p:Version=`, and is what the
extension reports to Bicep. Local builds use the placeholder `0.0.1-dev` version from
[`Bicep.Extension.Storage.csproj`](./src/Bicep.Extension.Storage/Bicep.Extension.Storage.csproj).

To configure this repository's GitHub branch protection and collaborators, login with the `gh` CLI and run:

```powershell
./scripts/setup.ps1
```

The script obtains a token from `GITHUB_TOKEN` or `gh auth token`, authenticates to GHCR
when Docker is available, and deploys [`scripts/repo/main.bicepparam`](./scripts/repo/main.bicepparam).
