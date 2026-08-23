using 'main.bicep'

// export STORAGE_ACCOUNT=<your ADLS Gen2 storage account name>
// export DATA_ENGINEERS_GROUP_ID=$(az ad group show --group data-engineers --query id -o tsv)
param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT')
param dataEngineersGroupId = readEnvironmentVariable('DATA_ENGINEERS_GROUP_ID')
