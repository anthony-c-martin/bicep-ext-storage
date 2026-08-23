targetScope = 'local'

@description('Name of the ADLS Gen2 (hierarchical namespace) storage account to manage.')
param storageAccountName string

@description('Microsoft Entra object ID of the group granted access to the curated zone.')
param dataEngineersGroupId string

extension storage with {
  accountName: storageAccountName
}

@description('A medallion layout, with one file system per zone.')
resource bronze 'DataLakeFileSystem' = {
  name: 'bronze'
  metadata: {
    zone: 'raw'
  }
}

resource silver 'DataLakeFileSystem' = {
  name: 'silver'
  metadata: {
    zone: 'cleansed'
  }
}

resource gold 'DataLakeFileSystem' = {
  name: 'gold'
  metadata: {
    zone: 'curated'
  }
  // Default entries are inherited by directories and files created later.
  accessControl: [
    {
      type: 'User'
      permissions: 'rwx'
    }
    {
      type: 'Group'
      permissions: 'r-x'
    }
    {
      type: 'Other'
      permissions: '---'
    }
    {
      scope: 'Default'
      type: 'Group'
      id: dataEngineersGroupId
      permissions: 'r-x'
    }
    {
      scope: 'Default'
      type: 'Mask'
      permissions: 'rwx'
    }
  ]
}

resource landing 'DataLakeDirectory' = {
  fileSystemName: bronze.name
  path: 'landing/events'
  permissions: '0750'
}

resource curatedSales 'DataLakeDirectory' = {
  fileSystemName: gold.name
  path: 'sales'
  accessControl: [
    {
      type: 'User'
      permissions: 'rwx'
    }
    {
      type: 'Group'
      id: dataEngineersGroupId
      permissions: 'r-x'
    }
    {
      type: 'Other'
      permissions: '---'
    }
    {
      type: 'Mask'
      permissions: 'r-x'
    }
  ]
}

output bronzeUrl string? = bronze.url
output landingUrl string? = landing.url
output curatedSalesUrl string? = curatedSales.url
