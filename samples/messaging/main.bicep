targetScope = 'local'

@description('Name of the storage account to manage.')
param storageAccountName string

@secure()
@description('Storage account shared key. Required because the table ACL API rejects Microsoft Entra tokens.')
param storageAccountKey string

extension storage with {
  accountName: storageAccountName
  accountKey: storageAccountKey
}

resource orders 'Queue' = {
  name: 'orders'
  metadata: {
    consumer: 'order-processor'
  }
}

resource ordersDeadLetter 'Queue' = {
  name: 'orders-poison'
  metadata: {
    consumer: 'order-processor'
    purpose: 'dead-letter'
  }
}

resource settings 'Table' = {
  name: 'settings'
  accessPolicies: [
    {
      id: 'readonly'
      permissions: 'r'
      start: '2026-01-01T00:00:00Z'
      expiry: '2027-01-01T00:00:00Z'
    }
  ]
}

@description('Seed configuration that the application reads at startup.')
resource orderLimits 'TableEntity' = {
  tableName: settings.name
  partitionKey: 'orders'
  rowKey: 'limits'
  entity: {
    maxItemsPerOrder: '50'
    maxOrderValue: '2500.00'
    requiresApproval: 'true'
    updatedOn: '2026-01-01T00:00:00Z'
  }
  // Values default to Edm.String, so declare the ones that need a richer type.
  entityTypes: {
    maxItemsPerOrder: 'Int32'
    maxOrderValue: 'Double'
    requiresApproval: 'Boolean'
    updatedOn: 'DateTime'
  }
}

resource orderRegions 'TableEntity' = {
  tableName: settings.name
  partitionKey: 'orders'
  rowKey: 'regions'
  entity: {
    primary: 'westeurope'
    secondary: 'northeurope'
  }
}

output ordersQueueUrl string? = orders.url
output deadLetterQueueUrl string? = ordersDeadLetter.url
output settingsTableUrl string? = settings.url
