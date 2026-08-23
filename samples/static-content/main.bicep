targetScope = 'local'

@description('Name of the storage account to manage.')
param storageAccountName string

extension storage with {
  accountName: storageAccountName
}

@description('Files to publish, mapped to the content type they are served with.')
var files = {
  'index.html': 'text/html'
}

resource site 'BlobContainer' = {
  name: 'public-site'
  publicAccess: 'Blob'
}

resource landingPage 'Blob' = {
  containerName: site.name
  name: 'index.html'
  contentType: files['index.html']
  cacheControl: 'public, max-age=300'
  // Uploads the file from disk rather than embedding its content in the template.
  contentBase64: loadFileAsBase64('./assets/index.html')
}

resource logs 'BlobContainer' = {
  name: 'site-logs'
  metadata: {
    retention: '30d'
  }
}

@description('Append blobs suit log-style content that is added to over time.')
resource accessLog 'Blob' = {
  containerName: logs.name
  name: 'access/current.log'
  type: 'Append'
  contentType: 'text/plain'
  content: '# access log initialised by Bicep\n'
}

@description('Archive rarely-read content to keep storage costs down.')
resource archivedPage 'Blob' = {
  containerName: logs.name
  name: 'archive/index-v1.html'
  accessTier: 'Cool'
  contentType: files['index.html']
  contentBase64: loadFileAsBase64('./assets/index.html')
}

output landingPageUrl string? = landingPage.url
output accessLogUrl string? = accessLog.url
output archivedPageUrl string? = archivedPage.url
