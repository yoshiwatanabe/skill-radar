# 🚀 GitHub Actions Setup Guide

## 🔐 Configure GitHub Repository Secrets

Go to your GitHub repository: https://github.com/yoshiwatanabe/skill-radar

**Settings** → **Secrets and variables** → **Actions** → **New repository secret**

### Required Secrets (Azure Authentication)
Add these secrets using the service principal you created:

1. **AZURE_CLIENT_ID**
   ```
   [Your service principal client ID from Azure CLI output]
   ```

2. **AZURE_TENANT_ID**
   ```
   [Your Azure tenant ID from Azure CLI output]
   ```

3. **AZURE_SUBSCRIPTION_ID**
   ```
   [Your Azure subscription ID from Azure CLI output]
   ```

4. **AZURE_CLIENT_SECRET**
   ```
   [Your service principal client secret from Azure CLI output]
   ```

### Required Secrets (API Keys)
5. **OPENAI_API_KEY**
   ```
   [Your new OpenAI API key]
   ```

### Required Secrets (Azure Resources)
6. **AZURE_RESOURCE_GROUP**
   ```
   skillradar-rg
   ```

7. **AZURE_STORAGE_ACCOUNT_NAME**
   ```
   skillradardevstorage
   ```

8. **AZURE_KEY_VAULT_NAME**
   ```
   skillradar-dev-kv
   ```

### Optional Secrets (Enhanced Data Collection)
9. **NEWS_API_KEY** (when you get it)
   ```
   [Your NewsAPI key]
   ```

10. **REDDIT_CLIENT_ID** (when you get it)
    ```
    [Your Reddit API client ID]
    ```

11. **REDDIT_CLIENT_SECRET** (when you get it)
    ```
    [Your Reddit API client secret]
    ```

## 🧪 Test the GitHub Actions Deployment

### Method 1: Manual Trigger
1. Go to **Actions** tab in your GitHub repository
2. Click **"Application Build & Deploy"** workflow
3. Click **"Run workflow"** button
4. Select **"main"** branch
5. Click **"Run workflow"**

### Method 2: Push Code Changes (Auto-trigger)
1. Make any change to files in `src/` directory
2. Commit and push to main branch
3. GitHub Actions will automatically trigger

### Method 3: Scheduled Execution (Sundays 9AM JST)
- The workflow is scheduled to run every Sunday at 9:00 AM JST
- No manual intervention needed

## 📊 Monitor Execution

### GitHub Actions Logs
1. Go to **Actions** tab
2. Click on the running/completed workflow
3. Click on job steps to see detailed logs
4. Look for:
   - ✅ Build completion
   - ✅ Container deployment
   - ✅ SkillRadar execution logs

### Azure Resource Monitoring
1. **Container Instances**: Check execution logs
   ```bash
   az container logs --resource-group skillradar-rg --name skillradar-dev-aci
   ```

2. **Storage Account**: Check for generated reports
   - Portal: Storage Account → Containers → reports
   - CLI: `az storage blob list --container-name reports --account-name skillradardevstorage`

3. **Key Vault**: Monitor access logs
   - Portal: Key Vault → Monitoring → Logs

## 🐛 Troubleshooting

### Common Issues & Solutions

**❌ GitHub Actions fails with authentication error**
- Check all Azure secrets are correctly set
- Verify service principal has contributor role on resource group

**❌ "Container deployment failed"**
- Check if container image exists (workflow builds it)
- Verify Azure Container Instance permissions

**❌ "No articles collected"**
- Normal if running outside of scheduled time
- Check network connectivity
- Verify API keys are accessible from Azure

**❌ "OpenAI API errors"**
- Check API key validity and billing
- Monitor rate limits

**❌ Build errors about Console.WriteLine**
- Should be fixed in latest version
- All Console references use System.Console.WriteLine

### Debug Commands

```bash
# Check Azure resources
az resource list --resource-group skillradar-rg --output table

# Test Key Vault access
az keyvault secret show --vault-name skillradar-dev-kv --name openai-api-key

# Check storage containers
az storage container list --account-name skillradardevstorage --auth-mode login

# Monitor container logs
az container logs --resource-group skillradar-rg --name skillradar-dev-aci-latest
```

## 📈 Expected Results

### Successful Execution Should Show:
1. **Build Phase**:
   - ✅ .NET application compiled
   - ✅ Docker image built and pushed
   - ✅ No compilation errors

2. **Deployment Phase**:
   - ✅ Container instance created/updated
   - ✅ Environment variables set
   - ✅ Container starts successfully

3. **Execution Phase**:
   - ✅ Articles collected from Hacker News
   - ✅ AI analysis completed
   - ✅ Reports generated
   - ✅ Files saved to storage

### Sample Successful Output:
```
🔍 SkillRadar - Weekly Technology Trend Analysis
================================================

⚙️  Loaded environment variables from Azure
📅 Analyzing trends for week: Jun 8 - Jun 14, 2025

📰 Collecting articles from multiple sources...
✅ Collected 15 articles

🔍 Analyzing trends and generating insights...
✅ Analysis complete

📊 Generating report...
✅ Report saved to Azure Storage

🎉 SkillRadar analysis complete!
```

## 🎯 Success Criteria

- [ ] All GitHub secrets configured
- [ ] GitHub Actions workflow runs without errors
- [ ] Container deploys successfully 
- [ ] SkillRadar generates weekly report
- [ ] Reports stored in Azure Storage
- [ ] Scheduled execution works (Sundays)

Once all criteria are met, SkillRadar will automatically generate weekly technology trend reports every Sunday! 🎉

## 📋 Quick Reference

**Your actual secret values** are provided in the SECRETS_TEMPLATE.md file (excluded from git for security).

Copy each value from that template to the corresponding GitHub secret above.