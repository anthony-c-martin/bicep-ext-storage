using 'main.bicep'

// export STORAGE_ACCOUNT=<your storage account name>
param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT')
param containerName = 'bicep-basic'
