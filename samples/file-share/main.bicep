targetScope = 'local'

@description('Name of the storage account to manage.')
param storageAccountName string

extension storage with {
  accountName: storageAccountName
}

resource share 'FileShare' = {
  name: 'app-config'
  quota: 100
  accessTier: 'TransactionOptimized'
  metadata: {
    managedBy: 'bicep'
  }
}

@description('Parent directories must exist before their children, so create them in order.')
resource configDirectory 'FileShareDirectory' = {
  shareName: share.name
  path: 'config'
}

resource environmentDirectory 'FileShareDirectory' = {
  shareName: share.name
  path: '${configDirectory.path}/production'
}

resource appSettings 'FileShareFile' = {
  shareName: share.name
  path: '${environmentDirectory.path}/appsettings.json'
  contentType: 'application/json'
  content: string({
    logging: {
      level: 'Information'
    }
    features: {
      betaSearch: false
    }
  })
  metadata: {
    generatedBy: 'bicep'
  }
}

output shareUrl string? = share.url
output appSettingsUrl string? = appSettings.url
