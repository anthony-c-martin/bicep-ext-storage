targetScope = 'local'

@description('Name of the storage account to manage.')
param storageAccountName string

@description('Name of the blob container to create.')
param containerName string

extension storage with {
  accountName: storageAccountName
}

resource container 'BlobContainer' = {
  name: containerName
  metadata: {
    managedBy: 'bicep'
  }
}

resource settings 'Blob' = {
  containerName: container.name
  name: 'config/settings.json'
  contentType: 'application/json'
  content: string({
    environment: 'dev'
    featureFlags: [
      'search'
    ]
  })
  tags: {
    project: 'bicep-ext-storage'
  }
}

output containerUrl string? = container.url
output settingsUrl string? = settings.url
