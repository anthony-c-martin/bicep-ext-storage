# Agent Instructions

## Model Authoring
* Bicep model classes should follow standard C# naming conventions, with properties defined using PascalCase. This ensures the types shown in Bicep are using camelCase, even if the underlying API follows a different convention (e.g. snake_case).
* Enum members are serialized verbatim, so the literals shown in Bicep match the C# member names exactly (e.g. `TransactionOptimized`).
* Only `string`, `int`, `bool`, enums, arrays, `Dictionary<string, T>` and nested classes can be represented in the Bicep type system. There is no `long` - model large values in a coarser unit (e.g. `sizeInMb`) rather than as a string.

## Iterating
To iterate on changes to the extensions (e.g. changing or adding models or handlers), use the following flow:
* Make changes
* Build the solution using `dotnet build .`. This'll typically run a lot faster for catching C# errors than runnig a full publish.
* Run `dotnet test`. `TypeDefinitionTests` generates the Bicep type definition, so unsupported property types fail here rather than at runtime.
* Run `./scripts/publish.ps1 ./bicep-ext-storage` to publish the self-contained extension to `./bicep-ext-storage`.
* `./samples/bicepconfig.json` already points at the local extension:
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
* Check the samples for warnings or errors by running `bicep lint --pattern './samples/**/*.bicepparam'`. Note that the error code BCP427 is expected, as expected env variables are not set - ignore this.

## Verifying end-to-end
Blob, queue and table resources can be verified without an Azure subscription by running against Azurite:
```sh
azurite --skipApiVersionCheck --location /tmp/azurite
```
Point the extension at `http://127.0.0.1:10000/devstoreaccount1` (blob), `:10001` (queue) and `:10002` (table) with the well-known `devstoreaccount1` key. Always deploy twice: the SDK `CreateIfNotExistsAsync` methods return a **null response object** (not a null `Value`) when the resource already exists, and only a second deployment catches mistakes in that reconciliation path. Azurite does not emulate the Files or Data Lake endpoints.

To verify those against real Azure, ask the user to select a sample to run, and be very clear that this will actually interact with the external environment, and can potentially be destructive - blobs and files that share a name with a declared resource are overwritten. If running into errors, it should be possible to troubleshoot by turning on verbose tracing with the env var `BICEP_TRACING_ENABLED` set to `true`.

After making changes, if relevant, add or update samples.
