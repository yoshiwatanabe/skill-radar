@description('Location for all resources')
param location string = resourceGroup().location

@description('Base name for all resources')
param baseName string = 'skillradar'

@description('Environment name (dev, staging, prod)')
param environment string = 'dev'

@secure()
@description('OpenAI API Key')
param openAiApiKey string

@secure()
@description('News API Key')
param newsApiKey string = ''

@secure()
@description('Reddit Client ID')
param redditClientId string = ''

@secure()
@description('Reddit Client Secret')
param redditClientSecret string = ''

@description('Container image for the console app')
param containerImage string = 'skillradar/console:latest'

var resourceNamePrefix = '${baseName}-${environment}'
var storageAccountName = '${replace(resourceNamePrefix, '-', '')}storage'
var keyVaultName = '${resourceNamePrefix}-kv'
var containerGroupName = '${resourceNamePrefix}-aci'
var logAnalyticsWorkspaceName = '${resourceNamePrefix}-logs'

// Log Analytics Workspace
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: logAnalyticsWorkspaceName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Storage Account for data persistence
module storage 'modules/storage.bicep' = {
  name: 'storage-deployment'
  params: {
    storageAccountName: storageAccountName
    location: location
  }
}

// Key Vault for secrets management
module keyVault 'modules/keyvault.bicep' = {
  name: 'keyvault-deployment'
  params: {
    keyVaultName: keyVaultName
    location: location
    openAiApiKey: openAiApiKey
    newsApiKey: newsApiKey
    redditClientId: redditClientId
    redditClientSecret: redditClientSecret
  }
}

// Container Instance for scheduled execution
resource containerGroup 'Microsoft.ContainerInstance/containerGroups@2023-05-01' = {
  name: containerGroupName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    containers: [
      {
        name: 'skillradar-console'
        properties: {
          image: containerImage
          resources: {
            requests: {
              cpu: 1
              memoryInGB: 2
            }
          }
          environmentVariables: [
            {
              name: 'AZURE_STORAGE_CONNECTION_STRING'
              secureValue: storage.outputs.connectionString
            }
            {
              name: 'AZURE_KEYVAULT_URI'
              value: keyVault.outputs.keyVaultUri
            }
            {
              name: 'OPENAI_API_KEY'
              secureValue: openAiApiKey
            }
            {
              name: 'NEWS_API_KEY'
              secureValue: newsApiKey
            }
            {
              name: 'REDDIT_CLIENT_ID'
              secureValue: redditClientId
            }
            {
              name: 'REDDIT_CLIENT_SECRET'
              secureValue: redditClientSecret
            }
          ]
        }
      }
    ]
    osType: 'Linux'
    restartPolicy: 'OnFailure'
    diagnostics: {
      logAnalytics: {
        workspaceId: logAnalyticsWorkspace.properties.customerId
        workspaceKey: logAnalyticsWorkspace.listKeys().primarySharedKey
      }
    }
  }
}

// Give Container Instance access to Key Vault
resource keyVaultAccessPolicy 'Microsoft.KeyVault/vaults/accessPolicies@2023-07-01' = {
  name: '${keyVaultName}/add'
  properties: {
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: containerGroup.identity.principalId
        permissions: {
          secrets: [
            'get'
            'list'
          ]
        }
      }
    ]
  }
}

// Logic App for weekly scheduling (optional)
resource logicApp 'Microsoft.Logic/workflows@2019-05-01' = {
  name: '${resourceNamePrefix}-scheduler'
  location: location
  properties: {
    state: 'Enabled'
    definition: {
      '$schema': 'https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#'
      contentVersion: '1.0.0.0'
      parameters: {}
      triggers: {
        Weekly_Schedule: {
          recurrence: {
            frequency: 'Week'
            interval: 1
            schedule: {
              weekDays: ['Sunday']
              hours: [9]
              minutes: [0]
            }
            timeZone: 'Tokyo Standard Time'
          }
          type: 'Recurrence'
        }
      }
      actions: {
        Start_Container_Instance: {
          type: 'Http'
          inputs: {
            method: 'POST'
            uri: '${az.environment().resourceManager}subscriptions/${subscription().subscriptionId}/resourceGroups/${resourceGroup().name}/providers/Microsoft.ContainerInstance/containerGroups/${containerGroupName}/start?api-version=2023-05-01'
            authentication: {
              type: 'ManagedServiceIdentity'
            }
          }
        }
      }
    }
  }
  identity: {
    type: 'SystemAssigned'
  }
}

// Role assignment for Logic App to manage Container Instances
resource logicAppRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(resourceGroup().id, logicApp.id, 'Contributor')
  scope: containerGroup
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b24988ac-6180-42a0-ab88-20f7382dd24c') // Contributor role
    principalId: logicApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

@description('Storage Account name')
output storageAccountName string = storage.outputs.storageAccountName

@description('Key Vault name')
output keyVaultName string = keyVault.outputs.keyVaultName

@description('Key Vault URI')
output keyVaultUri string = keyVault.outputs.keyVaultUri

@description('Container Group name')
output containerGroupName string = containerGroup.name

@description('Logic App name')
output logicAppName string = logicApp.name