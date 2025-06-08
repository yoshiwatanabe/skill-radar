#!/bin/bash

# SkillRadar Infrastructure Deployment Script
# This script deploys the SkillRadar Azure infrastructure using Bicep templates

set -e

# Configuration
RESOURCE_GROUP_NAME="skillradar-rg"
LOCATION="japaneast"
ENVIRONMENT="dev"
DEPLOYMENT_NAME="skillradar-deployment-$(date +%Y%m%d-%H%M%S)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Function to print colored output
print_status() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# Function to check if Azure CLI is installed and logged in
check_prerequisites() {
    print_status "Checking prerequisites..."
    
    # Check if Azure CLI is installed
    if ! command -v az &> /dev/null; then
        print_error "Azure CLI is not installed. Please install it first."
        exit 1
    fi
    
    # Check if logged in to Azure
    if ! az account show &> /dev/null; then
        print_error "You are not logged in to Azure. Please run 'az login' first."
        exit 1
    fi
    
    print_success "Prerequisites check passed"
}

# Function to validate required parameters
validate_parameters() {
    print_status "Validating parameters..."
    
    if [ -z "$OPENAI_API_KEY" ]; then
        print_error "OPENAI_API_KEY environment variable is required"
        exit 1
    fi
    
    print_success "Parameter validation passed"
}

# Function to create resource group
create_resource_group() {
    print_status "Creating resource group: $RESOURCE_GROUP_NAME"
    
    if az group show --name "$RESOURCE_GROUP_NAME" --output none 2>/dev/null; then
        print_warning "Resource group $RESOURCE_GROUP_NAME already exists"
    else
        az group create \
            --name "$RESOURCE_GROUP_NAME" \
            --location "$LOCATION" \
            --output none
        print_success "Resource group created successfully"
    fi
}

# Function to deploy Bicep template
deploy_infrastructure() {
    print_status "Deploying infrastructure..."
    
    local bicep_file="../bicep/main.bicep"
    
    # Build deployment parameters
    local params=(
        --resource-group "$RESOURCE_GROUP_NAME"
        --name "$DEPLOYMENT_NAME"
        --template-file "$bicep_file"
        --parameters environment="$ENVIRONMENT"
        --parameters openAiApiKey="$OPENAI_API_KEY"
    )
    
    # Add optional parameters if they exist
    if [ -n "$NEWS_API_KEY" ]; then
        params+=(--parameters newsApiKey="$NEWS_API_KEY")
    fi
    
    if [ -n "$REDDIT_CLIENT_ID" ]; then
        params+=(--parameters redditClientId="$REDDIT_CLIENT_ID")
    fi
    
    if [ -n "$REDDIT_CLIENT_SECRET" ]; then
        params+=(--parameters redditClientSecret="$REDDIT_CLIENT_SECRET")
    fi
    
    # Execute deployment
    print_status "Executing Bicep deployment..."
    az deployment group create "${params[@]}" --output table
    
    if [ $? -eq 0 ]; then
        print_success "Infrastructure deployment completed successfully"
    else
        print_error "Infrastructure deployment failed"
        exit 1
    fi
}

# Function to get deployment outputs
get_deployment_outputs() {
    print_status "Retrieving deployment outputs..."
    
    local outputs=$(az deployment group show \
        --resource-group "$RESOURCE_GROUP_NAME" \
        --name "$DEPLOYMENT_NAME" \
        --query properties.outputs \
        --output json)
    
    if [ $? -eq 0 ] && [ "$outputs" != "null" ]; then
        echo "$outputs" | jq -r '
            "Storage Account: " + .storageAccountName.value,
            "Key Vault: " + .keyVaultName.value,
            "Key Vault URI: " + .keyVaultUri.value,
            "Container Group: " + .containerGroupName.value,
            "Logic App: " + .logicAppName.value
        '
        print_success "Deployment outputs retrieved successfully"
    else
        print_warning "No deployment outputs found"
    fi
}

# Function to display usage
usage() {
    echo "Usage: $0 [options]"
    echo ""
    echo "Environment Variables (Required):"
    echo "  OPENAI_API_KEY       OpenAI API key for AI analysis"
    echo ""
    echo "Environment Variables (Optional):"
    echo "  NEWS_API_KEY         NewsAPI key for news collection"
    echo "  REDDIT_CLIENT_ID     Reddit API client ID"
    echo "  REDDIT_CLIENT_SECRET Reddit API client secret"
    echo ""
    echo "Options:"
    echo "  -h, --help          Show this help message"
    echo "  -g, --resource-group Override default resource group name"
    echo "  -l, --location      Override default location"
    echo "  -e, --environment   Override default environment (dev/staging/prod)"
    echo ""
    echo "Examples:"
    echo "  export OPENAI_API_KEY='your-openai-key'"
    echo "  export NEWS_API_KEY='your-newsapi-key'"
    echo "  $0"
    echo ""
    echo "  $0 --resource-group 'my-skillradar-rg' --location 'eastus'"
}

# Parse command line arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            usage
            exit 0
            ;;
        -g|--resource-group)
            RESOURCE_GROUP_NAME="$2"
            shift 2
            ;;
        -l|--location)
            LOCATION="$2"
            shift 2
            ;;
        -e|--environment)
            ENVIRONMENT="$2"
            shift 2
            ;;
        *)
            print_error "Unknown option: $1"
            usage
            exit 1
            ;;
    esac
done

# Main execution
main() {
    print_status "Starting SkillRadar infrastructure deployment"
    print_status "Resource Group: $RESOURCE_GROUP_NAME"
    print_status "Location: $LOCATION"
    print_status "Environment: $ENVIRONMENT"
    echo ""
    
    check_prerequisites
    validate_parameters
    create_resource_group
    deploy_infrastructure
    get_deployment_outputs
    
    echo ""
    print_success "SkillRadar infrastructure deployment completed!"
    print_status "Next steps:"
    echo "  1. Build and push the SkillRadar console application Docker image"
    echo "  2. Update the container image parameter in the Bicep template"
    echo "  3. Test the weekly scheduled execution"
    echo ""
    print_status "To delete the infrastructure, run:"
    echo "  az group delete --name $RESOURCE_GROUP_NAME --yes --no-wait"
}

# Check if script is being sourced or executed
if [[ "${BASH_SOURCE[0]}" == "${0}" ]]; then
    main "$@"
fi