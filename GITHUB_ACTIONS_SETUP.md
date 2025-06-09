# 🚀 GitHub Actions Setup Guide

## 🔐 Configure GitHub Repository Secrets

Go to your GitHub repository: https://github.com/yoshiwatanabe/skill-radar

**Settings** → **Secrets and variables** → **Actions** → **New repository secret**

### Required Secrets (Azure Authentication)
Add this secret using the service principal you created:

1. **AZURE_CREDENTIALS** (JSON format)
   ```json
   {
     "clientId": "[Your service principal client ID]",
     "clientSecret": "[Your service principal client secret]", 
     "subscriptionId": "[Your Azure subscription ID]",
     "tenantId": "[Your Azure tenant ID]"
   }
   ```

### Required Secrets (API Keys)
2. **OPENAI_API_KEY**
   ```
   [Your new OpenAI API key]
   ```

### Required Secrets (Azure Resources)
3. **AZURE_RESOURCE_GROUP**
   ```
   skillradar-rg
   ```

4. **AZURE_STORAGE_ACCOUNT_NAME**
   ```
   skillradardevstorage
   ```

5. **AZURE_KEY_VAULT_NAME**
   ```
   skillradar-dev-kv
   ```

### Optional Secrets (Enhanced Data Collection)
6. **NEWS_API_KEY** (when you get it)
   ```
   [Your NewsAPI key]
   ```

7. **REDDIT_CLIENT_ID** (when you get it)
   ```
   [Your Reddit API client ID]
   ```

8. **REDDIT_CLIENT_SECRET** (when you get it)
   ```
   [Your Reddit API client secret]
   ```

### Optional Secrets (Email Notifications)
9. **AZURE_COMMUNICATION_CONNECTION_STRING** (for email reports)
   ```
   endpoint=https://skillradar-communication.unitedstates.communication.azure.com/;accesskey=...
   ```

10. **EMAIL_SENDER_ADDRESS** (Azure Communication Services sender)
    ```
    DoNotReply@33b94393-dbd1-42d1-baaf-520e954318a0.azurecomm.net
    ```

11. **EMAIL_RECIPIENT_ADDRESS** (your email for reports)
    ```
    your-email@domain.com
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

### Method 3: Scheduled Execution (Thursdays 11PM JST)
- The workflow is scheduled to run every Thursday at 11:00 PM JST (ready for Friday learning)
- Runs automatically via cron: '0 14 * * 4' (14:00 UTC Thursday = 11:00 PM JST Thursday)
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
- Ensure AZURE_CREDENTIALS secret is set with correct JSON format
- Verify service principal has contributor role on resource group
- Double-check all values in the JSON credentials are correct

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
   - ✅ Articles collected from Hacker News, Reddit, NewsAPI
   - ✅ AI analysis and translation completed
   - ✅ Reports generated (Console, HTML, JSON, Markdown)
   - ✅ Email notification sent with visual dashboard
   - ✅ Files saved to storage
   - ✅ Japanese translations included (if enabled)

### Sample Successful Output:
```
🔍 SkillRadar - Weekly Technology Trend Analysis
================================================

⚙️  Loaded configuration from appsettings.json
📧 Azure Communication Services initialized with connection string
📅 Analyzing trends for week: Jun 8 - Jun 14, 2025

📰 Collecting articles from multiple sources...
✅ Collected 298 articles

🔍 Analyzing trends and generating insights...
✅ Analysis complete

📊 Generating report...
📧 Sending email notification...
✅ Email notification sent successfully!
🌐 Email includes JA translations

🎉 SkillRadar analysis complete!
📈 Processed 298 articles
🔥 Identified 5 trending topics
📚 Selected 10 must-read articles
```

## 🎯 Success Criteria

- [ ] All GitHub secrets configured
- [ ] GitHub Actions workflow runs without errors
- [ ] Container deploys successfully 
- [ ] SkillRadar generates weekly report
- [ ] Reports stored in Azure Storage
- [ ] Email notifications sent successfully
- [ ] Japanese translations work (if enabled)
- [ ] Scheduled execution works (Thursdays 11PM JST)

Once all criteria are met, SkillRadar will automatically generate weekly technology trend reports with beautiful email notifications every Thursday night! 🎉

## 📋 Quick Reference

**Your actual secret values** are provided in the SECRETS_TEMPLATE.md file (excluded from git for security).

Copy each value from that template to the corresponding GitHub secret above.