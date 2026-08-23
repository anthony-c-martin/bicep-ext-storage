using 'main.bicep'

// export STORAGE_ACCOUNT=<your storage account name>
// export STORAGE_ACCOUNT_KEY=$(az storage account keys list --account-name "$STORAGE_ACCOUNT" --query '[0].value' -o tsv)
param storageAccountName = readEnvironmentVariable('STORAGE_ACCOUNT')
param storageAccountKey = readEnvironmentVariable('STORAGE_ACCOUNT_KEY')
