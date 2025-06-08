@description('Key Vault name')
param keyVaultName string

@description('Location for the Key Vault')
param location string

@secure()
@description('OpenAI API Key')
param openAiApiKey string

@secure()
@description('News API Key')
param newsApiKey string

@secure()
@description('Reddit Client ID')
param redditClientId string

@secure()
@description('Reddit Client Secret')
param redditClientSecret string

@description('Object ID of the current user (for initial access)')
param userObjectId string = ''

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: false
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: false
    accessPolicies: userObjectId != '' ? [
      {
        tenantId: subscription().tenantId
        objectId: userObjectId
        permissions: {
          secrets: [
            'all'
          ]
          keys: [
            'all'
          ]
        }
      }
    ] : []
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// Store API keys as secrets
resource openAiSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'openai-api-key'
  parent: keyVault
  properties: {
    value: openAiApiKey
    attributes: {
      enabled: true
    }
  }
}

resource newsApiSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (newsApiKey != '') {
  name: 'news-api-key'
  parent: keyVault
  properties: {
    value: newsApiKey
    attributes: {
      enabled: true
    }
  }
}

resource redditClientIdSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (redditClientId != '') {
  name: 'reddit-client-id'
  parent: keyVault
  properties: {
    value: redditClientId
    attributes: {
      enabled: true
    }
  }
}

resource redditClientSecretSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = if (redditClientSecret != '') {
  name: 'reddit-client-secret'
  parent: keyVault
  properties: {
    value: redditClientSecret
    attributes: {
      enabled: true
    }
  }
}

@description('Key Vault name')
output keyVaultName string = keyVault.name

@description('Key Vault resource ID')
output keyVaultId string = keyVault.id

@description('Key Vault URI')
output keyVaultUri string = keyVault.properties.vaultUri