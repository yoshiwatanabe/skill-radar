using System;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using SkillRadar.Console.Models;
using SkillRadar.Console.Services;

namespace SkillRadar.Console
{
    class Program
    {
        // Fixed Console namespace conflicts for GitHub Actions CI/CD
        static async Task Main(string[] args)
        {
            System.Console.WriteLine("🔍 SkillRadar - Weekly Technology Trend Analysis");
            System.Console.WriteLine("================================================");
            System.Console.WriteLine();

            // Load environment variables from .env file
            EnvironmentLoader.LoadFromFile();
            System.Console.WriteLine();

            try
            {
                var config = await LoadConfigurationAsync();
                
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("User-Agent", "SkillRadar/1.0 (Technology Trend Analyzer)");

                var newsService = new NewsCollectionService(
                    httpClient,
                    Environment.GetEnvironmentVariable("NEWS_API_KEY"),
                    Environment.GetEnvironmentVariable("REDDIT_CLIENT_ID"),
                    Environment.GetEnvironmentVariable("REDDIT_CLIENT_SECRET")
                );

                var analysisService = new TrendAnalysisService(
                    httpClient,
                    Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                );

                // Initialize Azure Storage service if connection string or account name is available
                AzureStorageService? azureStorageService = null;
                var azureStorageConnectionString = Environment.GetEnvironmentVariable("AZURE_STORAGE_CONNECTION_STRING");
                var azureStorageAccountName = Environment.GetEnvironmentVariable("AZURE_STORAGE_ACCOUNT_NAME");
                
                var storageCredential = azureStorageConnectionString ?? azureStorageAccountName;
                if (!string.IsNullOrEmpty(storageCredential))
                {
                    try
                    {
                        azureStorageService = new AzureStorageService(storageCredential);
                        System.Console.WriteLine($"⚙️  Azure Storage initialized using {(azureStorageConnectionString != null ? "connection string" : "managed identity")}");
                        await azureStorageService.TestConnectionAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"⚠️  Azure Storage initialization failed: {ex.Message}");
                        System.Console.WriteLine("📝 Reports will be saved locally only");
                        azureStorageService = null;
                    }
                }
                else
                {
                    System.Console.WriteLine("⚠️  Neither AZURE_STORAGE_CONNECTION_STRING nor AZURE_STORAGE_ACCOUNT_NAME found");
                    System.Console.WriteLine("📝 Reports will be saved locally only");
                }

                var reportService = new ReportGenerationService(azureStorageService);

                var weekStart = GetWeekStart();
                var weekEnd = GetWeekEnd(weekStart);

                System.Console.WriteLine($"📅 Analyzing trends for week: {weekStart:MMM d} - {weekEnd:MMM d, yyyy}");
                System.Console.WriteLine();

                System.Console.WriteLine("📰 Collecting articles from multiple sources...");
                var articles = await newsService.CollectWeeklyArticlesAsync(weekStart, weekEnd);
                System.Console.WriteLine($"✅ Collected {articles.Count} articles");

                // Save articles to Azure Storage if available
                if (azureStorageService != null && articles.Count > 0)
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var articlesFileName = $"articles_{timestamp}.json";
                    await azureStorageService.UploadArticlesAsync(articles, articlesFileName);
                }

                System.Console.WriteLine();

                if (articles.Count == 0)
                {
                    System.Console.WriteLine("⚠️  No articles found for this week. Please check your API keys and network connectivity.");
                    return;
                }

                System.Console.WriteLine("🔍 Analyzing trends and generating insights...");
                var trendReport = await analysisService.AnalyzeWeeklyTrendsAsync(articles, config.UserProfile);
                System.Console.WriteLine("✅ Analysis complete");

                // Save trend report to Azure Storage if available
                if (azureStorageService != null)
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var trendReportFileName = $"trend_analysis_{timestamp}.json";
                    await azureStorageService.UploadTrendReportAsync(trendReport, trendReportFileName);
                }

                System.Console.WriteLine();

                System.Console.WriteLine("📊 Generating report...");
                System.Console.WriteLine();

                if (config.ReportSettings.OutputFormats.Contains("Console"))
                {
                    await reportService.GenerateConsoleReportAsync(trendReport);
                }

                if (config.ReportSettings.OutputFormats.Contains("File"))
                {
                    var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    var markdownPath = $"skill_radar_report_{timestamp}.md";
                    var htmlPath = $"skill_radar_report_{timestamp}.html";
                    var jsonPath = $"skill_radar_report_{timestamp}.json";

                    await reportService.GenerateFileReportAsync(trendReport, markdownPath, "markdown");
                    await reportService.GenerateFileReportAsync(trendReport, htmlPath, "html");
                    await reportService.GenerateFileReportAsync(trendReport, jsonPath, "json");
                }

                System.Console.WriteLine();
                System.Console.WriteLine("🎉 SkillRadar analysis complete!");
                System.Console.WriteLine($"📈 Processed {articles.Count} articles");
                System.Console.WriteLine($"🔥 Identified {trendReport.TopTrends.Count} trending topics");
                System.Console.WriteLine($"📚 Selected {trendReport.MustReadArticles.Count} must-read articles");
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ Error: {ex.Message}");
                System.Console.WriteLine();
                System.Console.WriteLine("💡 Make sure to set the following environment variables:");
                System.Console.WriteLine("   - OPENAI_API_KEY (required for AI analysis)");
                System.Console.WriteLine("   - NEWS_API_KEY (optional, for NewsAPI integration)");
                System.Console.WriteLine("   - REDDIT_CLIENT_ID (optional, for Reddit API)");
                System.Console.WriteLine("   - REDDIT_CLIENT_SECRET (optional, for Reddit API)");
                
                if (args.Length > 0 && args[0] == "--debug")
                {
                    System.Console.WriteLine();
                    System.Console.WriteLine("🐛 Debug information:");
                    System.Console.WriteLine(ex.ToString());
                }
            }
        }

        private static async Task<AppConfiguration> LoadConfigurationAsync()
        {
            var configPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            
            if (File.Exists(configPath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(configPath);
                    var config = JsonSerializer.Deserialize<AppConfiguration>(json, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    if (config != null)
                    {
                        System.Console.WriteLine("⚙️  Loaded configuration from appsettings.json");
                        return config;
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine($"⚠️  Error loading configuration: {ex.Message}");
                }
            }

            System.Console.WriteLine("⚙️  Using default configuration");
            return GetDefaultConfiguration();
        }

        private static AppConfiguration GetDefaultConfiguration()
        {
            return new AppConfiguration
            {
                UserProfile = new UserProfile
                {
                    Skills = new[] { "C#", "Azure", "Machine Learning", "DevOps" }.ToList(),
                    Interests = new[] { "AI/ML", "Cloud Architecture", "Software Engineering" }.ToList(),
                    CareerStage = "Senior",
                    LearningGoals = new[] { "System Design", "AI Implementation", "Leadership" }.ToList()
                },
                DataSources = new Dictionary<string, DataSource>
                {
                    ["HackerNews"] = new DataSource { Enabled = true, Priority = "High" },
                    ["Reddit"] = new DataSource 
                    { 
                        Enabled = true, 
                        Priority = "Medium",
                        Subreddits = new[] { "programming", "MachineLearning", "dotnet", "AZURE", "devops" }.ToList()
                    },
                    ["NewsAPI"] = new DataSource { Enabled = true, Priority = "Low", Categories = new[] { "technology" }.ToList() }
                },
                ReportSettings = new ReportSettings
                {
                    MaxArticlesPerSource = 50,
                    TopTrendsCount = 5,
                    MustReadCount = 10,
                    OutputFormats = new[] { "Console", "File" }.ToList()
                }
            };
        }

        private static DateTime GetWeekStart()
        {
            var today = DateTime.Today;
            var daysSinceSunday = ((int)today.DayOfWeek - (int)DayOfWeek.Sunday + 7) % 7;
            return today.AddDays(-daysSinceSunday); // This past Sunday
        }

        private static DateTime GetWeekEnd(DateTime weekStart)
        {
            return weekStart.AddDays(6); // Following Saturday
        }
    }

    public class AppConfiguration
    {
        public UserProfile UserProfile { get; set; } = new UserProfile();
        public Dictionary<string, DataSource> DataSources { get; set; } = new Dictionary<string, DataSource>();
        public ReportSettings ReportSettings { get; set; } = new ReportSettings();
    }
}