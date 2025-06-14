#!/bin/bash

# Create static container for Logic App scheduling
echo "Creating static container: skillradar-dev-aci"

az container create \
  --resource-group skillradar-rg \
  --name skillradar-dev-aci \
  --image ghcr.io/yoshiwatanabe/skill-radar/skillradar-console:latest \
  --os-type Linux \
  --restart-policy Never \
  --environment-variables \
    OPENAI_API_KEY="${OPENAI_API_KEY}" \
    NEWS_API_KEY="${NEWS_API_KEY}" \
    REDDIT_CLIENT_ID="${REDDIT_CLIENT_ID}" \
    REDDIT_CLIENT_SECRET="${REDDIT_CLIENT_SECRET}" \
    AZURE_STORAGE_ACCOUNT_NAME="skillradardevstorage" \
    AZURE_COMMUNICATION_CONNECTION_STRING="${AZURE_COMMUNICATION_CONNECTION_STRING}" \
    EMAIL_SENDER_ADDRESS="${EMAIL_SENDER_ADDRESS}" \
    EMAIL_RECIPIENT_ADDRESS="${EMAIL_RECIPIENT_ADDRESS}" \
  --cpu 1 \
  --memory 2

echo "Static container created successfully!"
echo "Logic App should target: skillradar-dev-aci"