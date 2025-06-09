using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SkillRadar.Console.Models;

namespace SkillRadar.Console.Services
{
    public class NewsCollectionService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _newsApiKey;
        private readonly string? _redditClientId;
        private readonly string? _redditClientSecret;

        public NewsCollectionService(HttpClient httpClient, string? newsApiKey = null, string? redditClientId = null, string? redditClientSecret = null)
        {
            _httpClient = httpClient;
            _newsApiKey = newsApiKey;
            _redditClientId = redditClientId;
            _redditClientSecret = redditClientSecret;
        }

        public async Task<List<Article>> CollectWeeklyArticlesAsync(DateTime weekStart, DateTime weekEnd)
        {
            var allArticles = new List<Article>();

            var hackerNewsTask = CollectHackerNewsAsync(weekStart, weekEnd);
            var redditTask = CollectRedditArticlesAsync(weekStart, weekEnd);
            var newsApiTask = CollectNewsApiArticlesAsync(weekStart, weekEnd);

            var results = await Task.WhenAll(hackerNewsTask, redditTask, newsApiTask);

            allArticles.AddRange(results.SelectMany(x => x));

            return RemoveDuplicates(allArticles);
        }

        private async Task<List<Article>> CollectHackerNewsAsync(DateTime weekStart, DateTime weekEnd)
        {
            try
            {
                var articles = new List<Article>();
                
                var topStoriesResponse = await _httpClient.GetStringAsync("https://hacker-news.firebaseio.com/v0/topstories.json");
                var storyIds = JsonSerializer.Deserialize<int[]>(topStoriesResponse);

                if (storyIds == null) return articles;
                
                var tasks = storyIds.Take(50).Select(async id =>
                {
                    try
                    {
                        var storyResponse = await _httpClient.GetStringAsync($"https://hacker-news.firebaseio.com/v0/item/{id}.json");
                        var story = JsonSerializer.Deserialize<HackerNewsStory>(storyResponse);
                        
                        if (story != null && !string.IsNullOrEmpty(story.Title))
                        {
                            var publishedAt = story.Time.HasValue ? 
                                DateTimeOffset.FromUnixTimeSeconds(story.Time.Value).DateTime : DateTime.Now;
                            
                            // Filter articles by date range
                            if (publishedAt >= weekStart && publishedAt <= weekEnd)
                            {
                                return new Article
                                {
                                    Id = $"hn_{id}",
                                    Title = story.Title,
                                    Url = story.Url ?? $"https://news.ycombinator.com/item?id={id}",
                                    Source = "HackerNews",
                                    PublishedAt = publishedAt,
                                    Score = story.Score ?? 0,
                                    TechTags = ExtractTechTags(story.Title)
                                };
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error fetching HN story {id}: {ex.Message}");
                    }
                    return null;
                });

                var results = await Task.WhenAll(tasks);
                articles.AddRange(results.Where(a => a != null)!);
                
                return articles;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error collecting HackerNews articles: {ex.Message}");
                return new List<Article>();
            }
        }

        private async Task<List<Article>> CollectRedditArticlesAsync(DateTime weekStart, DateTime weekEnd)
        {
            try
            {
                var articles = new List<Article>();
                var subreddits = new[] { 
                    "programming", "MachineLearning", "dotnet", "azure", "devops", "softwarearchitecture",
                    "artificial", "ChatGPT", "OpenAI", "LocalLLaMA", "singularity"
                };

                foreach (var subreddit in subreddits)
                {
                    try
                    {
                        // Create request with proper headers for Reddit JSON API
                        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.reddit.com/r/{subreddit}/hot.json?limit=50");
                        request.Headers.Add("Accept", "application/json");
                        
                        var httpResponse = await _httpClient.SendAsync(request);
                        var response = await httpResponse.Content.ReadAsStringAsync();
                        
                        var jsonOptions = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        
                        var redditResponse = JsonSerializer.Deserialize<RedditResponse>(response, jsonOptions);

                        if (redditResponse?.Data?.Children != null)
                        {
                            foreach (var child in redditResponse.Data.Children)
                            {
                                var post = child.Data;
                                if (post != null && post.Created_utc.HasValue)
                                {
                                    var publishedAt = DateTimeOffset.FromUnixTimeSeconds((long)post.Created_utc.Value).DateTime;
                                    
                                    if (publishedAt >= weekStart && publishedAt <= weekEnd && !string.IsNullOrEmpty(post.Url))
                                    {
                                        articles.Add(new Article
                                        {
                                            Id = $"reddit_{post.Id}",
                                            Title = post.Title ?? "",
                                            Summary = post.Selftext ?? "",
                                            Url = post.Url,
                                            Source = $"Reddit-{subreddit}",
                                            PublishedAt = publishedAt,
                                            Score = post.Score ?? 0,
                                            TechTags = ExtractTechTags($"{post.Title} {post.Selftext}")
                                        });
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"Error fetching Reddit subreddit {subreddit}: {ex.Message}");
                    }
                }

                return articles;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error collecting Reddit articles: {ex.Message}");
                return new List<Article>();
            }
        }

        private async Task<List<Article>> CollectNewsApiArticlesAsync(DateTime weekStart, DateTime weekEnd)
        {
            if (string.IsNullOrEmpty(_newsApiKey))
            {
                System.Console.WriteLine("NewsAPI key not provided, skipping NewsAPI collection");
                return new List<Article>();
            }

            try
            {
                var articles = new List<Article>();
                var fromDate = weekStart.ToString("yyyy-MM-dd");
                var toDate = weekEnd.ToString("yyyy-MM-dd");
                
                // Enhanced AI-focused search queries (simplified syntax for better results)
                var queries = new[]
                {
                    // Core AI and Agent queries
                    "AI OR \"artificial intelligence\" OR \"machine learning\"",
                    "\"generative AI\" OR LLM OR \"language model\" OR ChatGPT OR Claude",
                    "\"AI agent\" OR \"agentic AI\" OR \"multi-agent\" OR OpenAI OR Anthropic",
                    
                    // Cloud and Platform Engineering
                    "Azure OR AWS OR \"cloud computing\" OR \"platform engineering\"",
                    "DevOps OR kubernetes OR serverless OR microservices",
                    
                    // Software Architecture and Development
                    "\"software architecture\" OR \"system design\" OR \"distributed systems\"",
                    "\".NET\" OR \"C#\" OR programming OR \"software development\""
                };
                
                // Top AI/Tech focused sources
                var sources = "techcrunch,ars-technica,the-verge,wired";
                
                // Enhanced domains targeting AI and tech publications
                var domains = "techcrunch.com,arstechnica.com,theverge.com,wired.com,venturebeat.com,thenextweb.com";
                
                foreach (var query in queries)
                {
                    try
                    {
                        var encodedQuery = Uri.EscapeDataString(query);
                        
                        // Try with preferred sources first, using broader date range for better results
                        var broadFromDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
                        var url = $"https://newsapi.org/v2/everything?q={encodedQuery}&domains={domains}&from={broadFromDate}&sortBy=popularity&searchIn=title,description&apiKey={_newsApiKey}&pageSize=15";
                        
                        var response = await _httpClient.GetStringAsync(url);
                        var newsResponse = JsonSerializer.Deserialize<NewsApiResponse>(response);

                        // If no results with domain restriction, try without domain restriction
                        if (newsResponse?.Articles == null || newsResponse.Articles.Length == 0)
                        {
                            url = $"https://newsapi.org/v2/everything?q={encodedQuery}&category=technology&from={broadFromDate}&sortBy=popularity&searchIn=title,description&apiKey={_newsApiKey}&pageSize=10";
                            response = await _httpClient.GetStringAsync(url);
                            newsResponse = JsonSerializer.Deserialize<NewsApiResponse>(response);
                        }

                        if (newsResponse?.Articles != null)
                        {
                            foreach (var article in newsResponse.Articles)
                            {
                                if (article.PublishedAt.HasValue && !string.IsNullOrEmpty(article.Url))
                                {
                                    // Avoid duplicates by checking if URL already exists
                                    if (!articles.Any(a => a.Url == article.Url))
                                    {
                                        articles.Add(new Article
                                        {
                                            Id = $"newsapi_{article.Url.GetHashCode()}",
                                            Title = article.Title ?? "",
                                            Summary = article.Description ?? "",
                                            Url = article.Url,
                                            Source = $"NewsAPI-{article.Source?.Name ?? "Unknown"}",
                                            PublishedAt = article.PublishedAt.Value,
                                            TechTags = ExtractTechTags($"{article.Title} {article.Description}")
                                        });
                                    }
                                }
                            }
                        }
                        
                        // Rate limiting: small delay between queries
                        await Task.Delay(200);
                    }
                    catch (Exception queryEx)
                    {
                        System.Console.WriteLine($"Error with query '{query}': {queryEx.Message}");
                    }
                }

                return articles.Take(30).ToList(); // Limit to 30 articles to maintain balance
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error collecting NewsAPI articles: {ex.Message}");
                return new List<Article>();
            }
        }

        private List<string> ExtractTechTags(string text)
        {
            var techKeywords = new[]
            {
                // AI & Machine Learning - Enhanced focus
                "AI", "Artificial Intelligence", "Machine Learning", "ML", "LLM", "Large Language Model",
                "GPT", "ChatGPT", "Claude", "Generative AI", "GenAI", "AI Agent", "Agentic AI", 
                "AI Agents", "Multi-Agent", "Agent Framework", "LangChain", "AutoGPT", "RAG",
                "Retrieval Augmented Generation", "Fine-tuning", "Prompt Engineering", "Vector Database",
                "Embeddings", "Transformer", "Neural Network", "Deep Learning", "NLP", "Computer Vision",
                
                // Cloud & Infrastructure  
                "Azure", "AWS", "GCP", "Cloud", "Azure OpenAI", "Azure AI", "OpenAI", "Anthropic",
                "Docker", "Kubernetes", "Container", "Serverless", "Microservices", "Infrastructure",
                "SRE", "Platform Engineering", "DevOps", "CI/CD", "Monitoring", "Observability",
                
                // Programming & Development
                "C#", ".NET", "ASP.NET", "Python", "JavaScript", "TypeScript", "Rust", "Go", "Java",
                "API", "REST", "GraphQL", "gRPC", "Database", "SQL", "NoSQL", "PostgreSQL", "Redis",
                
                // Architecture & Design
                "System Design", "Software Architecture", "Distributed Systems", "Event-Driven",
                "CQRS", "Event Sourcing", "Domain-Driven Design", "DDD", "Clean Architecture",
                "Performance", "Scalability", "High Availability", "Load Balancing",
                
                // Security & Best Practices
                "Security", "Cybersecurity", "Authentication", "Authorization", "OAuth", "JWT",
                "Zero Trust", "Identity", "RBAC", "Encryption", "TLS", "PKI",
                
                // Development Tools & Practices
                "Framework", "Library", "Open Source", "Git", "GitHub", "Deployment", "Testing",
                "Unit Testing", "Integration Testing", "TDD", "BDD", "Code Review", "Refactoring"
            };

            return techKeywords.Where(keyword => 
                text.Contains(keyword, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        private List<Article> RemoveDuplicates(List<Article> articles)
        {
            return articles
                .GroupBy(a => new { a.Url, NormalizedTitle = a.Title.Trim().ToLowerInvariant() })
                .Select(g => g.OrderByDescending(a => a.Score).First())
                .ToList();
        }

        // Debug methods to test each source separately
        public async Task<List<Article>> CollectHackerNewsDebugAsync(DateTime weekStart, DateTime weekEnd)
        {
            return await CollectHackerNewsAsync(weekStart, weekEnd);
        }

        public async Task<List<Article>> CollectRedditDebugAsync(DateTime weekStart, DateTime weekEnd)
        {
            try
            {
                var articles = new List<Article>();
                var subreddits = new[] { 
                    "programming", "MachineLearning", "dotnet", "azure", "devops", "softwarearchitecture",
                    "artificial", "ChatGPT", "OpenAI", "LocalLLaMA", "singularity"
                };

                System.Console.WriteLine($"🔍 Testing Reddit API access...");
                
                foreach (var subreddit in subreddits)
                {
                    try
                    {
                        System.Console.WriteLine($"  📡 Fetching from r/{subreddit}...");
                        
                        // Create request with proper headers for Reddit JSON API
                        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://www.reddit.com/r/{subreddit}/hot.json?limit=10");
                        request.Headers.Add("Accept", "application/json");
                        
                        var httpResponse = await _httpClient.SendAsync(request);
                        var response = await httpResponse.Content.ReadAsStringAsync();
                        
                        var jsonOptions = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        
                        var redditResponse = JsonSerializer.Deserialize<RedditResponse>(response, jsonOptions);

                        if (redditResponse?.Data?.Children != null && redditResponse.Data.Children.Length > 0)
                        {
                            System.Console.WriteLine($"  ✅ Got {redditResponse.Data.Children.Length} posts from r/{subreddit}");
                            
                            foreach (var child in redditResponse.Data.Children)
                            {
                                var post = child.Data;
                                if (post != null && post.Created_utc.HasValue)
                                {
                                    var publishedAt = DateTimeOffset.FromUnixTimeSeconds((long)post.Created_utc.Value).DateTime;
                                    
                                    System.Console.WriteLine($"    • {post.Title} (Published: {publishedAt:MMM d HH:mm})");
                                    
                                    // For debug, collect ALL articles regardless of date
                                    if (!string.IsNullOrEmpty(post.Url))
                                    {
                                        var article = new Article
                                        {
                                            Id = $"reddit_{post.Id}",
                                            Title = post.Title ?? "",
                                            Summary = post.Selftext ?? "",
                                            Url = post.Url,
                                            Source = $"Reddit r/{subreddit}",
                                            PublishedAt = publishedAt,
                                            Score = post.Score ?? 0,
                                            TechTags = ExtractTechTags($"{post.Title} {post.Selftext}")
                                        };
                                        articles.Add(article);
                                    }
                                }
                            }
                        }
                        else
                        {
                            System.Console.WriteLine($"  ❌ No data returned from r/{subreddit}");
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"  ❌ Error fetching r/{subreddit}: {ex.Message}");
                    }
                }

                System.Console.WriteLine($"📊 Total articles collected: {articles.Count}");
                return articles;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ Reddit debug failed: {ex.Message}");
                return new List<Article>();
            }
        }

        public async Task<List<Article>> CollectNewsApiDebugAsync(DateTime weekStart, DateTime weekEnd)
        {
            if (string.IsNullOrEmpty(_newsApiKey))
            {
                System.Console.WriteLine("❌ NewsAPI key not provided");
                return new List<Article>();
            }

            try
            {
                var articles = new List<Article>();
                var fromDate = weekStart.ToString("yyyy-MM-dd");
                var toDate = weekEnd.ToString("yyyy-MM-dd");
                
                System.Console.WriteLine($"🔍 Testing Enhanced NewsAPI access...");
                System.Console.WriteLine($"  📡 API Key: {_newsApiKey.Substring(0, 8)}...");
                System.Console.WriteLine($"  📅 Date range: {fromDate} to {toDate}");
                
                // Enhanced AI-focused search queries (simplified for better results)
                var testQueries = new[]
                {
                    "AI OR \"artificial intelligence\" OR \"machine learning\"",
                    "\"generative AI\" OR LLM OR ChatGPT OR Claude",
                    "Azure OR AWS OR \"cloud computing\" OR DevOps"
                };
                
                var sources = "techcrunch,ars-technica,the-verge,wired";
                var domains = "techcrunch.com,arstechnica.com,theverge.com,wired.com";
                
                foreach (var query in testQueries)
                {
                    try
                    {
                        System.Console.WriteLine($"  🔍 Testing query: {query}");
                        
                        var encodedQuery = Uri.EscapeDataString(query);
                        
                        // Try with broader date range (last 7 days) for better results
                        var broadFromDate = DateTime.Now.AddDays(-7).ToString("yyyy-MM-dd");
                        var url = $"https://newsapi.org/v2/everything?q={encodedQuery}&domains={domains}&from={broadFromDate}&sortBy=popularity&searchIn=title,description&apiKey={_newsApiKey}&pageSize=5";
                        
                        System.Console.WriteLine($"  🌐 URL: {url.Replace(_newsApiKey, "***API_KEY***")}");
                        
                        var response = await _httpClient.GetStringAsync(url);
                        System.Console.WriteLine($"  📄 Response length: {response.Length} characters");
                        
                        var jsonOptions = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        };
                        
                        var newsResponse = JsonSerializer.Deserialize<NewsApiResponse>(response, jsonOptions);

                        // If no results, try without domain restriction
                        if (newsResponse?.Articles == null || newsResponse.Articles.Length == 0)
                        {
                            System.Console.WriteLine($"  🔄 Trying without domain restrictions...");
                            url = $"https://newsapi.org/v2/everything?q={encodedQuery}&category=technology&from={broadFromDate}&sortBy=popularity&searchIn=title,description&apiKey={_newsApiKey}&pageSize=5";
                            response = await _httpClient.GetStringAsync(url);
                            newsResponse = JsonSerializer.Deserialize<NewsApiResponse>(response, jsonOptions);
                            System.Console.WriteLine($"  📄 Fallback response length: {response.Length} characters");
                        }

                        if (newsResponse?.Articles != null && newsResponse.Articles.Length > 0)
                        {
                            System.Console.WriteLine($"  ✅ Got {newsResponse.Articles.Length} articles for this query");
                            
                            foreach (var article in newsResponse.Articles)
                            {
                                if (article.PublishedAt.HasValue && !string.IsNullOrEmpty(article.Url))
                                {
                                    System.Console.WriteLine($"    • {article.Title} (Published: {article.PublishedAt:MMM d HH:mm})");
                                    System.Console.WriteLine($"      Source: {article.Source?.Name}, URL: {article.Url}");
                                    
                                    // Avoid duplicates
                                    if (!articles.Any(a => a.Url == article.Url))
                                    {
                                        var newArticle = new Article
                                        {
                                            Id = $"newsapi_{article.Url.GetHashCode()}",
                                            Title = article.Title ?? "",
                                            Summary = article.Description ?? "",
                                            Url = article.Url,
                                            Source = article.Source?.Name ?? "NewsAPI",
                                            PublishedAt = article.PublishedAt.Value,
                                            Score = 0,
                                            TechTags = ExtractTechTags($"{article.Title} {article.Description}")
                                        };
                                        articles.Add(newArticle);
                                    }
                                }
                            }
                        }
                        else
                        {
                            System.Console.WriteLine($"  ⚠️ No results for this query");
                        }
                        
                        System.Console.WriteLine();
                        await Task.Delay(100); // Rate limiting
                    }
                    catch (Exception queryEx)
                    {
                        System.Console.WriteLine($"  ❌ Error with query '{query}': {queryEx.Message}");
                    }
                }

                System.Console.WriteLine($"📊 Total unique articles collected: {articles.Count}");
                
                if (articles.Count > 0)
                {
                    System.Console.WriteLine($"\n🏷️  Top Tech Tags from NewsAPI:");
                    var topTags = articles.SelectMany(a => a.TechTags).GroupBy(t => t).OrderByDescending(g => g.Count()).Take(5);
                    foreach (var tag in topTags)
                    {
                        System.Console.WriteLine($"  {tag.Key}: {tag.Count()} mentions");
                    }
                }
                
                return articles;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"❌ NewsAPI debug failed: {ex.Message}");
                if (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
                {
                    System.Console.WriteLine("  🔑 This looks like an API key authentication issue");
                }
                return new List<Article>();
            }
        }
    }

    public class HackerNewsStory
    {
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        
        [JsonPropertyName("url")]
        public string? Url { get; set; }
        
        [JsonPropertyName("score")]
        public int? Score { get; set; }
        
        [JsonPropertyName("time")]
        public long? Time { get; set; }
    }

    public class RedditResponse
    {
        public RedditData? Data { get; set; }
    }

    public class RedditData
    {
        public RedditChild[]? Children { get; set; }
    }

    public class RedditChild
    {
        public RedditPost? Data { get; set; }
    }

    public class RedditPost
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Selftext { get; set; }
        public string? Url { get; set; }
        public int? Score { get; set; }
        public double? Created_utc { get; set; }
    }

    public class NewsApiResponse
    {
        public NewsApiArticle[]? Articles { get; set; }
    }

    public class NewsApiArticle
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Url { get; set; }
        public DateTime? PublishedAt { get; set; }
        public NewsApiSource? Source { get; set; }
    }

    public class NewsApiSource
    {
        public string? Name { get; set; }
    }
}