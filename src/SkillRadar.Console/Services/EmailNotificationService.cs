using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure;
using Azure.Communication.Email;
using Azure.Identity;
using SkillRadar.Console.Models;

namespace SkillRadar.Console.Services
{
    public class EmailNotificationService
    {
        private readonly EmailClient? _emailClient;
        private readonly string? _senderAddress;
        private readonly string? _recipientAddress;

        public EmailNotificationService(string? connectionString = null, string? senderAddress = null, string? recipientAddress = null)
        {
            _senderAddress = senderAddress;
            _recipientAddress = recipientAddress;

            if (!string.IsNullOrEmpty(connectionString))
            {
                try
                {
                    _emailClient = new EmailClient(connectionString);
                    System.Console.WriteLine("📧 Azure Communication Services initialized with connection string");
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"⚠️  Failed to initialize email client with connection string: {ex.Message}");
                }
            }
            else
            {
                System.Console.WriteLine("⚠️  No Azure Communication Services connection string provided");
                System.Console.WriteLine("📝 Email notifications will be disabled");
            }
        }

        public async Task<bool> SendWeeklyReportAsync(TrendReport report, List<Article> articles, string secondaryLanguage = "None")
        {
            if (_emailClient == null || string.IsNullOrEmpty(_senderAddress) || string.IsNullOrEmpty(_recipientAddress))
            {
                System.Console.WriteLine("📧 Email service not configured, skipping email notification");
                return false;
            }

            var subject = $"SkillRadar Weekly Analysis - {report.WeekStart:MMM d} to {report.WeekEnd:MMM d, yyyy}";
            var htmlBody = await GenerateEmailHtmlAsync(report, articles, secondaryLanguage);

            try
            {

                var emailMessage = new EmailMessage(
                    senderAddress: _senderAddress,
                    content: new EmailContent(subject)
                    {
                        Html = htmlBody
                    },
                    recipients: new EmailRecipients(new List<EmailAddress> { new EmailAddress(_recipientAddress) }));

                System.Console.WriteLine($"📧 Sending weekly report email to {_recipientAddress}...");
                
                // Wait for email to be fully processed by Azure Communication Services
                var operation = await _emailClient.SendAsync(WaitUntil.Completed, emailMessage);
                
                // Additional verification of the operation status
                await WaitForEmailDeliveryAsync(operation, "weekly report");
                
                System.Console.WriteLine($"✅ Email sent and processed successfully! Message ID: {operation.Id}");
                System.Console.WriteLine($"📧 Status: {operation.Value.Status}");
                
                return true;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ Failed to send weekly report email: {ex.Message}");
                
                // Retry once with a delay
                System.Console.WriteLine("🔄 Retrying email send in 10 seconds...");
                await Task.Delay(TimeSpan.FromSeconds(10));
                
                try
                {
                    System.Console.WriteLine("📧 Attempting to resend weekly report email...");
                    var retryOperation = await _emailClient.SendAsync(WaitUntil.Completed, new EmailMessage(
                        senderAddress: _senderAddress,
                        content: new EmailContent(subject) { Html = htmlBody },
                        recipients: new EmailRecipients(new List<EmailAddress> { new EmailAddress(_recipientAddress) })));
                    
                    await WaitForEmailDeliveryAsync(retryOperation, "weekly report (retry)");
                    System.Console.WriteLine($"✅ Weekly report email sent successfully on retry! Message ID: {retryOperation.Id}");
                    return true;
                }
                catch (Exception retryEx)
                {
                    System.Console.WriteLine($"❌ Retry also failed: {retryEx.Message}");
                    return false;
                }
            }
        }

        public async Task<bool> SendErrorNotificationAsync(string errorMessage, string context = "")
        {
            if (_emailClient == null || string.IsNullOrEmpty(_senderAddress) || string.IsNullOrEmpty(_recipientAddress))
            {
                return false;
            }

            try
            {
                var subject = "SkillRadar Analysis Failed";
                var htmlBody = GenerateErrorEmailHtml(errorMessage, context);

                var emailMessage = new EmailMessage(
                    senderAddress: _senderAddress,
                    content: new EmailContent(subject)
                    {
                        Html = htmlBody
                    },
                    recipients: new EmailRecipients(new List<EmailAddress> { new EmailAddress(_recipientAddress) }));

                System.Console.WriteLine($"📧 Sending error notification email to {_recipientAddress}...");
                
                // Wait for email to be fully processed by Azure Communication Services
                var operation = await _emailClient.SendAsync(WaitUntil.Completed, emailMessage);
                
                // Additional verification of the operation status
                await WaitForEmailDeliveryAsync(operation, "error notification");
                
                System.Console.WriteLine($"✅ Error notification sent and processed successfully! Message ID: {operation.Id}");
                System.Console.WriteLine($"📧 Status: {operation.Value.Status}");
                
                return true;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ Failed to send error notification: {ex.Message}");
                return false;
            }
        }

        private async Task WaitForEmailDeliveryAsync(Azure.Operation<EmailSendResult> operation, string emailType)
        {
            try
            {
                // Give additional time for email processing
                System.Console.WriteLine($"🕒 Waiting for {emailType} email processing to complete...");
                await Task.Delay(TimeSpan.FromSeconds(5));
                
                // Check if operation completed successfully
                if (operation.HasCompleted)
                {
                    var result = operation.Value;
                    System.Console.WriteLine($"📊 Email operation completed with status: {result.Status}");
                    
                    if (result.Status == EmailSendStatus.Succeeded)
                    {
                        System.Console.WriteLine($"🎉 {emailType} email delivered successfully!");
                    }
                    else
                    {
                        System.Console.WriteLine($"⚠️  {emailType} email status: {result.Status}");
                        if (!string.IsNullOrEmpty(result.Error?.Message))
                        {
                            System.Console.WriteLine($"📝 Error details: {result.Error.Message}");
                        }
                    }
                }
                else
                {
                    System.Console.WriteLine($"⏳ {emailType} email operation still in progress...");
                }
                
                // Additional delay to ensure delivery
                System.Console.WriteLine("🕒 Adding extra delay to ensure email delivery...");
                await Task.Delay(TimeSpan.FromSeconds(3));
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"⚠️  Could not verify {emailType} email delivery status: {ex.Message}");
            }
        }

        private async Task<string> GenerateEmailHtmlAsync(TrendReport report, List<Article> articles, string secondaryLanguage = "None")
        {
            var html = new StringBuilder();
            
            // Initialize translation service if secondary language is requested
            TranslationService? translationService = null;
            if (!secondaryLanguage.Equals("None", StringComparison.OrdinalIgnoreCase))
            {
                translationService = new TranslationService(new HttpClient(), Environment.GetEnvironmentVariable("OPENAI_API_KEY"));
            }
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            html.AppendLine("<title>SkillRadar Weekly Report</title>");
            html.AppendLine("<style>");
            html.AppendLine(@"
                body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 20px; background-color: #f5f5f5; }
                .container { max-width: 800px; margin: 0 auto; background: white; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); overflow: hidden; }
                .header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; text-align: center; }
                .header h1 { margin: 0; font-size: 28px; font-weight: 300; }
                .header p { margin: 10px 0 0 0; opacity: 0.9; }
                .content { padding: 30px; }
                .section { margin-bottom: 30px; }
                .section h2 { color: #4a5568; border-bottom: 2px solid #e2e8f0; padding-bottom: 10px; margin-bottom: 20px; }
                .trend-item { background: #f7fafc; border-left: 4px solid #667eea; padding: 15px; margin-bottom: 15px; border-radius: 0 6px 6px 0; }
                .trend-name { font-weight: 600; color: #2d3748; font-size: 18px; }
                .trend-count { color: #667eea; font-weight: 500; }
                .trend-insight { margin: 8px 0; color: #4a5568; }
                .article-item { border: 1px solid #e2e8f0; border-radius: 6px; padding: 15px; margin-bottom: 12px; transition: box-shadow 0.2s; }
                .article-item:hover { box-shadow: 0 2px 8px rgba(0,0,0,0.1); }
                .article-title { font-weight: 600; margin-bottom: 5px; }
                .article-title a { color: #2d3748; text-decoration: none; }
                .article-title a:hover { color: #667eea; }
                .article-meta { font-size: 12px; color: #718096; margin-bottom: 8px; }
                .article-relevance { background: #667eea; color: white; padding: 2px 8px; border-radius: 12px; font-size: 11px; display: inline-block; }
                .tags { margin-top: 8px; }
                .tag { background: #edf2f7; color: #4a5568; padding: 2px 8px; border-radius: 4px; font-size: 11px; margin-right: 5px; display: inline-block; }
                .stats { display: flex; justify-content: space-around; background: #f7fafc; padding: 20px; border-radius: 6px; margin: 20px 0; }
                .stat { text-align: center; }
                .stat-number { font-size: 24px; font-weight: 600; color: #667eea; }
                .stat-label { font-size: 12px; color: #718096; margin-top: 5px; }
                .footer { background: #f7fafc; padding: 20px; text-align: center; color: #718096; font-size: 12px; }
                .learning-rec { background: #e6fffa; border: 1px solid #81e6d9; padding: 12px; border-radius: 6px; margin: 10px 0; color: #234e52; }
            ");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");
            
            // Header
            html.AppendLine("<div class='container'>");
            html.AppendLine("<div class='header'>");
            html.AppendLine("<h1>🔍 SkillRadar Weekly Report</h1>");
            html.AppendLine($"<p>{report.WeekStart:MMMM d} - {report.WeekEnd:MMMM d, yyyy}</p>");
            html.AppendLine("</div>");
            
            // Content
            html.AppendLine("<div class='content'>");
            
            // Stats Summary
            html.AppendLine("<div class='stats'>");
            html.AppendLine($"<div class='stat'><div class='stat-number'>{articles.Count}</div><div class='stat-label'>Articles Analyzed</div></div>");
            html.AppendLine($"<div class='stat'><div class='stat-number'>{report.TopTrends.Count}</div><div class='stat-label'>Trending Topics</div></div>");
            html.AppendLine($"<div class='stat'><div class='stat-number'>{report.MustReadArticles.Count}</div><div class='stat-label'>Must-Read Articles</div></div>");
            html.AppendLine("</div>");
            
            // Weekly Summary
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h2>📋 Weekly Summary</h2>");
            html.AppendLine($"<p>{report.WeeklySummary}</p>");
            html.AppendLine("</div>");
            
            // Top Trends - Visual Dashboard
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h2>🔥 Trending Technologies This Week</h2>");
            html.AppendLine("<p style='color: #718096; margin-bottom: 25px; font-size: 14px;'>Most mentioned technologies across 290+ articles from leading tech sources</p>");
            
            var maxCount = report.TopTrends.Take(5).Max(t => t.MentionCount);
            foreach (var (trend, index) in report.TopTrends.Take(5).Select((t, i) => (t, i)))
            {
                var percentage = (double)trend.MentionCount / maxCount * 100;
                var barColor = index switch
                {
                    0 => "#667eea", // Top trend - primary color
                    1 => "#764ba2", // Second - secondary color  
                    2 => "#f093fb", // Third - gradient color
                    3 => "#f5576c", // Fourth - accent color
                    _ => "#a8a8a8"   // Fifth - neutral color
                };
                
                html.AppendLine("<div style='margin-bottom: 18px;'>");
                html.AppendLine($"<div style='margin-bottom: 6px;'>");
                html.AppendLine($"<span style='font-weight: 600; color: #2d3748; font-size: 16px;'>#{index + 1} {trend.Name} ({trend.MentionCount} mentions)</span>");
                html.AppendLine("</div>");
                html.AppendLine($"<div style='background: #f1f1f1; border-radius: 8px; height: 12px; overflow: hidden;'>");
                html.AppendLine($"<div style='background: linear-gradient(90deg, {barColor}, {barColor}aa); height: 100%; width: {percentage}%; transition: width 0.3s ease;'></div>");
                html.AppendLine("</div>");
                html.AppendLine("</div>");
            }
            html.AppendLine("</div>");
            
            // Must-Read Articles
            html.AppendLine("<div class='section'>");
            html.AppendLine("<h2>📚 Must-Read Articles</h2>");
            foreach (var article in report.MustReadArticles.Take(8))
            {
                html.AppendLine("<div class='article-item'>");
                html.AppendLine($"<div class='article-title'><a href='{article.Url}' target='_blank'>{article.Title}</a></div>");
                
                // Add Japanese translation if enabled
                if (translationService != null)
                {
                    var (titleTranslation, summaryTranslation) = await translationService.TranslateArticleAsync(article.Title, article.Summary ?? "", secondaryLanguage);
                    if (!string.IsNullOrEmpty(titleTranslation))
                    {
                        html.AppendLine($"<div class='article-title-translation' style='font-size: 14px; color: #718096; margin: 4px 0; font-style: italic;'>🇯🇵 {titleTranslation}</div>");
                    }
                }
                
                html.AppendLine($"<div class='article-meta'>{article.Source} • {article.PublishedAt:MMM d, yyyy} • <span class='article-relevance'>{article.RelevanceScore:P0} relevant</span></div>");
                if (!string.IsNullOrEmpty(article.Summary))
                {
                    // Limit preview to 150 characters for consistent article heights
                    var preview = article.Summary.Length > 150 ? article.Summary.Substring(0, 150) + "..." : article.Summary;
                    html.AppendLine($"<div style='margin: 8px 0; color: #4a5568;'>{preview}</div>");
                    
                    // Add Japanese summary translation if enabled
                    if (translationService != null)
                    {
                        var summaryForTranslation = article.Summary.Length > 150 ? article.Summary.Substring(0, 150) : article.Summary;
                        var summaryTranslation = await translationService.TranslateAsync(summaryForTranslation, secondaryLanguage);
                        if (!string.IsNullOrEmpty(summaryTranslation))
                        {
                            html.AppendLine($"<div style='margin: 8px 0; color: #9ca3af; font-size: 13px; font-style: italic;'>🇯🇵 {summaryTranslation}</div>");
                        }
                    }
                }
                if (article.TechTags.Any())
                {
                    html.AppendLine("<div class='tags'>");
                    foreach (var tag in article.TechTags.Take(5))
                    {
                        html.AppendLine($"<span class='tag'>{tag}</span>");
                    }
                    html.AppendLine("</div>");
                }
                html.AppendLine("</div>");
            }
            html.AppendLine("</div>");
            
            // Learning recommendations are now integrated into the trending dashboard
            
            html.AppendLine("</div>");
            
            // Footer
            html.AppendLine("<div class='footer'>");
            html.AppendLine($"<p>Generated on {DateTime.Now:yyyy-MM-dd HH:mm} UTC by SkillRadar</p>");
            html.AppendLine("<p>🤖 Powered by AI-driven technology trend analysis</p>");
            html.AppendLine("</div>");
            
            html.AppendLine("</div>");
            html.AppendLine("</body></html>");
            
            return html.ToString();
        }

        private string GenerateErrorEmailHtml(string errorMessage, string context)
        {
            var html = new StringBuilder();
            
            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html><head>");
            html.AppendLine("<meta charset='utf-8'>");
            html.AppendLine("<style>");
            html.AppendLine(@"
                body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #333; margin: 0; padding: 20px; background-color: #f5f5f5; }
                .container { max-width: 600px; margin: 0 auto; background: white; border-radius: 8px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); overflow: hidden; }
                .header { background: #e53e3e; color: white; padding: 30px; text-align: center; }
                .content { padding: 30px; }
                .error-message { background: #fed7d7; border: 1px solid #fc8181; padding: 15px; border-radius: 6px; margin: 15px 0; }
            ");
            html.AppendLine("</style>");
            html.AppendLine("</head><body>");
            
            html.AppendLine("<div class='container'>");
            html.AppendLine("<div class='header'>");
            html.AppendLine("<h1>❌ SkillRadar Analysis Failed</h1>");
            html.AppendLine($"<p>{DateTime.Now:MMMM d, yyyy HH:mm} UTC</p>");
            html.AppendLine("</div>");
            
            html.AppendLine("<div class='content'>");
            html.AppendLine("<p>The weekly SkillRadar analysis encountered an error and could not complete.</p>");
            
            if (!string.IsNullOrEmpty(context))
            {
                html.AppendLine($"<p><strong>Context:</strong> {context}</p>");
            }
            
            html.AppendLine("<div class='error-message'>");
            html.AppendLine($"<strong>Error Details:</strong><br>{errorMessage}");
            html.AppendLine("</div>");
            
            html.AppendLine("<p>Please check the GitHub Actions logs for more detailed information.</p>");
            html.AppendLine("</div>");
            
            html.AppendLine("</div>");
            html.AppendLine("</body></html>");
            
            return html.ToString();
        }
    }
}