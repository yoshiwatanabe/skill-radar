using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using SkillRadar.Console.Models;

namespace SkillRadar.Console.Services
{
    public class TrendAnalysisService
    {
        private readonly HttpClient _httpClient;
        private readonly string? _openAiApiKey;

        public TrendAnalysisService(HttpClient httpClient, string? openAiApiKey = null)
        {
            _httpClient = httpClient;
            _openAiApiKey = openAiApiKey;
        }

        public async Task<TrendReport> AnalyzeWeeklyTrendsAsync(List<Article> articles, UserProfile userProfile)
        {
            var weekStart = articles.Min(a => a.PublishedAt).Date;
            var weekEnd = articles.Max(a => a.PublishedAt).Date;

            var techKeywordFrequency = AnalyzeTechKeywords(articles);
            var topTrends = await IdentifyTopTrendsAsync(articles, techKeywordFrequency, userProfile);
            var mustReadArticles = SelectMustReadArticles(articles, userProfile, 10);
            var learningRecommendations = await GenerateLearningRecommendationsAsync(topTrends, userProfile);
            var weeklySummary = await GenerateWeeklySummaryAsync(articles, topTrends, userProfile);

            return new TrendReport
            {
                WeekStart = weekStart,
                WeekEnd = weekEnd,
                TopTrends = topTrends,
                MustReadArticles = mustReadArticles,
                LearningRecommendations = learningRecommendations,
                TechKeywordFrequency = techKeywordFrequency,
                WeeklySummary = weeklySummary
            };
        }

        private Dictionary<string, int> AnalyzeTechKeywords(List<Article> articles)
        {
            var keywordFrequency = new Dictionary<string, int>();

            foreach (var article in articles)
            {
                foreach (var tag in article.TechTags)
                {
                    var normalizedTag = tag.ToLowerInvariant();
                    keywordFrequency[normalizedTag] = keywordFrequency.GetValueOrDefault(normalizedTag, 0) + 1;
                }

                var titleWords = ExtractKeywordsFromText(article.Title);
                var summaryWords = ExtractKeywordsFromText(article.Summary);
                
                foreach (var word in titleWords.Concat(summaryWords))
                {
                    keywordFrequency[word] = keywordFrequency.GetValueOrDefault(word, 0) + 1;
                }
            }

            return keywordFrequency
                .Where(kv => kv.Value >= 3) // Increase threshold for more meaningful trends
                .OrderByDescending(kv => kv.Value)
                .Take(50)
                .ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        private async Task<List<TrendingTopic>> IdentifyTopTrendsAsync(List<Article> articles, Dictionary<string, int> keywordFrequency, UserProfile userProfile)
        {
            // Filter out overly generic terms that aren't useful for senior engineers
            var genericTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ai", "go", "api", "git", "github", "rest", "testing", "programming", 
                "development", "software", "technology", "code", "system", "data"
            };

            // Focus on compound/specific trends and meaningful technologies
            var meaningfulKeywords = keywordFrequency
                .Where(kv => !genericTerms.Contains(kv.Key))
                .OrderByDescending(kv => kv.Value)
                .Take(15) // Take more to have options after filtering
                .ToList();

            // Add compound trend detection for specific AI/tech topics
            var compoundTrends = DetectCompoundTrends(articles);
            
            // Merge compound trends with keyword trends
            var allTrends = new Dictionary<string, int>();
            foreach (var trend in meaningfulKeywords)
                allTrends[trend.Key] = trend.Value;
            foreach (var trend in compoundTrends)
                allTrends[trend.Key] = trend.Value;

            var topTrends = allTrends.OrderByDescending(kv => kv.Value).Take(10).ToList();
            var trendingTopics = new List<TrendingTopic>();

            foreach (var keyword in topTrends)
            {
                var relatedArticles = articles
                    .Where(a => a.TechTags.Any(tag => tag.ToLowerInvariant() == keyword.Key) ||
                               a.Title.Contains(keyword.Key, StringComparison.OrdinalIgnoreCase) ||
                               a.Summary.Contains(keyword.Key, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(a => a.Score)
                    .Take(5)
                    .ToList();

                if (relatedArticles.Any())
                {
                    var insight = await GenerateKeyInsightAsync(keyword.Key, relatedArticles);
                    var recommendation = await GenerateLearningRecommendationAsync(keyword.Key, userProfile);

                    trendingTopics.Add(new TrendingTopic
                    {
                        Name = keyword.Key,
                        MentionCount = keyword.Value,
                        KeyInsight = insight,
                        LearningRecommendation = recommendation,
                        RelatedArticles = relatedArticles
                    });
                }
            }

            return trendingTopics.Take(5).ToList();
        }

        private Dictionary<string, int> DetectCompoundTrends(List<Article> articles)
        {
            var compoundTrends = new Dictionary<string, int>();
            
            // Define specific compound trends we care about
            var compoundPatterns = new Dictionary<string, string[]>
            {
                ["AI Agents"] = new[] { "ai agent", "agentic ai", "ai agents", "autonomous agent", "agent framework" },
                ["Vector Database"] = new[] { "vector database", "vector db", "pinecone", "weaviate", "chroma", "qdrant" },
                ["RAG Systems"] = new[] { "rag", "retrieval augmented", "retrieval-augmented generation", "rag pipeline" },
                ["LLM Engineering"] = new[] { "llm engineering", "prompt engineering", "fine-tuning", "model optimization", "llm ops" },
                ["Cloud Native"] = new[] { "cloud native", "cloud-native", "serverless architecture", "container orchestration" },
                ["Platform Engineering"] = new[] { "platform engineering", "developer experience", "internal platforms", "devex" },
                ["Real-time AI"] = new[] { "real-time ai", "streaming ai", "edge ai", "ai inference", "live ai" },
                ["AI Governance"] = new[] { "ai governance", "ai ethics", "responsible ai", "ai compliance", "ai safety" },
                ["Multimodal AI"] = new[] { "multimodal", "vision language", "vlm", "multimodal ai", "cross-modal" },
                ["Edge Computing"] = new[] { "edge computing", "edge ai", "iot edge", "distributed computing", "fog computing" }
            };
            
            foreach (var pattern in compoundPatterns)
            {
                var matchCount = 0;
                var compoundName = pattern.Key;
                var searchTerms = pattern.Value;
                
                foreach (var article in articles)
                {
                    var fullText = $"{article.Title} {article.Summary}".ToLowerInvariant();
                    
                    if (searchTerms.Any(term => fullText.Contains(term.ToLowerInvariant())))
                    {
                        matchCount++;
                    }
                }
                
                // Only include compound trends with meaningful mention counts
                if (matchCount >= 3)
                {
                    compoundTrends[compoundName] = matchCount;
                }
            }
            
            return compoundTrends;
        }

        private List<Article> SelectMustReadArticles(List<Article> articles, UserProfile userProfile, int count)
        {
            return articles
                .Select(article => new { Article = article, Relevance = CalculateRelevanceScore(article, userProfile) })
                .OrderByDescending(x => x.Relevance)
                .ThenByDescending(x => x.Article.Score)
                .Take(count)
                .Select(x => {
                    x.Article.RelevanceScore = x.Relevance;
                    return x.Article;
                })
                .ToList();
        }

        private float CalculateRelevanceScore(Article article, UserProfile userProfile)
        {
            var relevanceScore = 0f;

            foreach (var skill in userProfile.Skills)
            {
                if (article.Title.Contains(skill, StringComparison.OrdinalIgnoreCase) ||
                    article.Summary.Contains(skill, StringComparison.OrdinalIgnoreCase) ||
                    article.TechTags.Any(tag => tag.Equals(skill, StringComparison.OrdinalIgnoreCase)))
                {
                    relevanceScore += 0.3f;
                }
            }

            foreach (var interest in userProfile.Interests)
            {
                if (article.Title.Contains(interest, StringComparison.OrdinalIgnoreCase) ||
                    article.Summary.Contains(interest, StringComparison.OrdinalIgnoreCase))
                {
                    relevanceScore += 0.2f;
                }
            }

            foreach (var goal in userProfile.LearningGoals)
            {
                if (article.Title.Contains(goal, StringComparison.OrdinalIgnoreCase) ||
                    article.Summary.Contains(goal, StringComparison.OrdinalIgnoreCase))
                {
                    relevanceScore += 0.4f;
                }
            }

            var normalizedScore = (float)Math.Log(article.Score + 1) / 10f;
            relevanceScore += normalizedScore;

            return Math.Min(relevanceScore, 1.0f);
        }

        private async Task<string> GenerateKeyInsightAsync(string keyword, List<Article> relatedArticles)
        {
            if (string.IsNullOrEmpty(_openAiApiKey))
            {
                return $"Growing interest in {keyword} with {relatedArticles.Count} related articles this week";
            }

            try
            {
                var articlesText = string.Join("\n", relatedArticles.Take(3).Select(a => $"- {a.Title}: {a.Summary}"));
                var prompt = $@"Based on these recent articles about {keyword}:
{articlesText}

Generate a concise key insight (1-2 sentences) about the current trend or development in {keyword}. Focus on what's happening now and why it matters.";

                return await CallOpenAIAsync(prompt) ?? $"Growing interest in {keyword} with {relatedArticles.Count} related articles this week";
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error generating insight for {keyword}: {ex.Message}");
                return $"Growing interest in {keyword} with {relatedArticles.Count} related articles this week";
            }
        }

        private async Task<string> GenerateLearningRecommendationAsync(string keyword, UserProfile userProfile)
        {
            // Generate specific, actionable recommendations based on the keyword
            var specificRecommendations = new Dictionary<string, string>
            {
                ["AI Agents"] = "Build a multi-agent system using LangChain or AutoGen - start with a simple research assistant that coordinates multiple specialized agents",
                ["Vector Database"] = "Implement RAG with Pinecone or Weaviate - build a document Q&A system for your own knowledge base",
                ["RAG Systems"] = "Create a production RAG pipeline combining embeddings, vector search, and LLM completion for enterprise document search", 
                ["LLM Engineering"] = "Master prompt engineering and fine-tuning - experiment with few-shot learning and chain-of-thought prompting",
                ["Platform Engineering"] = "Design an internal developer platform using Backstage or Humanitec to improve team productivity",
                ["Cloud Native"] = "Implement serverless-first architecture with event-driven microservices on Azure Functions or AWS Lambda",
                ["Real-time AI"] = "Build real-time AI inference with streaming data using Azure Stream Analytics and edge deployment",
                ["AI Governance"] = "Establish AI model governance with MLOps pipelines, monitoring, and responsible AI practices",
                ["Multimodal AI"] = "Experiment with vision-language models for document understanding or image-text retrieval systems",
                ["Edge Computing"] = "Deploy AI models to edge devices using Azure IoT Edge or AWS Greengrass for low-latency inference"
            };

            if (specificRecommendations.ContainsKey(keyword))
            {
                return specificRecommendations[keyword];
            }

            if (string.IsNullOrEmpty(_openAiApiKey))
            {
                return $"Explore {keyword} through hands-on projects and real-world implementation";
            }

            try
            {
                var userContext = $"Skills: {string.Join(", ", userProfile.Skills)}, " +
                                $"Interests: {string.Join(", ", userProfile.Interests)}, " +
                                $"Career Stage: {userProfile.CareerStage}";

                var prompt = $@"Given a user profile: {userContext}
And trending technology: {keyword}

Suggest a specific, actionable weekend project or learning path (1 sentence) for this senior engineer to skill up in {keyword}. Focus on hands-on implementation, not theory.";

                return await CallOpenAIAsync(prompt) ?? $"Consider exploring {keyword} fundamentals and practical applications";
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error generating learning recommendation for {keyword}: {ex.Message}");
                return $"Consider exploring {keyword} fundamentals and practical applications";
            }
        }

        private async Task<List<string>> GenerateLearningRecommendationsAsync(List<TrendingTopic> topTrends, UserProfile userProfile)
        {
            var recommendations = new List<string>();

            foreach (var trend in topTrends.Take(3))
            {
                var recommendation = await GenerateDetailedLearningRecommendationAsync(trend, userProfile);
                recommendations.Add(recommendation);
            }

            return recommendations;
        }

        private async Task<string> GenerateDetailedLearningRecommendationAsync(TrendingTopic trend, UserProfile userProfile)
        {
            if (string.IsNullOrEmpty(_openAiApiKey))
            {
                return $"Deep dive: {trend.Name} (estimated 4-6 hours) - Focus on practical implementation";
            }

            try
            {
                var userContext = $"Skills: {string.Join(", ", userProfile.Skills)}, " +
                                $"Learning Goals: {string.Join(", ", userProfile.LearningGoals)}, " +
                                $"Career Stage: {userProfile.CareerStage}";

                var prompt = $@"User profile: {userContext}
Trending topic: {trend.Name} ({trend.MentionCount} mentions)
Key insight: {trend.KeyInsight}

Generate a specific weekly learning recommendation including:
- Focus area (be specific)
- Estimated time commitment
- Practical outcome

Format: 'Focus area: X (estimated Y hours) - Outcome description'";

                return await CallOpenAIAsync(prompt) ?? $"Deep dive: {trend.Name} (estimated 4-6 hours) - Focus on practical implementation";
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error generating detailed learning recommendation for {trend.Name}: {ex.Message}");
                return $"Deep dive: {trend.Name} (estimated 4-6 hours) - Focus on practical implementation";
            }
        }

        private async Task<string> GenerateWeeklySummaryAsync(List<Article> articles, List<TrendingTopic> topTrends, UserProfile userProfile)
        {
            if (string.IsNullOrEmpty(_openAiApiKey))
            {
                var topTrendNames = string.Join(", ", topTrends.Take(3).Select(t => t.Name));
                return $"This week's technology landscape focused on {topTrendNames}. Total {articles.Count} articles analyzed from various sources.";
            }

            try
            {
                var trendsText = string.Join("\n", topTrends.Take(3).Select(t => $"- {t.Name}: {t.KeyInsight}"));
                var prompt = $@"Weekly technology summary based on {articles.Count} articles:

Top trends:
{trendsText}

User interests: {string.Join(", ", userProfile.Interests)}

Generate a concise weekly summary (2-3 sentences) highlighting the most important technology developments this week and their potential impact.";

                return await CallOpenAIAsync(prompt) ?? $"This week's technology landscape focused on key developments across {topTrends.Count} major areas.";
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error generating weekly summary: {ex.Message}");
                return $"This week's technology landscape focused on key developments across {topTrends.Count} major areas.";
            }
        }

        private async Task<string?> CallOpenAIAsync(string prompt)
        {
            if (string.IsNullOrEmpty(_openAiApiKey))
            {
                return null;
            }

            try
            {
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiApiKey}");
                _httpClient.DefaultRequestHeaders.Add("User-Agent", "SkillRadar/1.0");

                var requestBody = new
                {
                    model = "gpt-3.5-turbo",
                    messages = new[]
                    {
                        new { role = "system", content = "You are a technology trend analyst. Provide concise, actionable insights." },
                        new { role = "user", content = prompt }
                    },
                    max_tokens = 150,
                    temperature = 0.7
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var openAiResponse = JsonSerializer.Deserialize<OpenAIResponse>(responseContent);
                    return openAiResponse?.Choices?.FirstOrDefault()?.Message?.Content?.Trim();
                }
                else
                {
                    System.Console.WriteLine($"OpenAI API error: {response.StatusCode}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"Error calling OpenAI API: {ex.Message}");
                return null;
            }
        }

        private List<string> ExtractKeywordsFromText(string text)
        {
            if (string.IsNullOrEmpty(text)) return new List<string>();

            var techKeywords = new[]
            {
                "ai", "artificial intelligence", "machine learning", "ml", "deep learning",
                "azure", "aws", "gcp", "cloud", "serverless", "kubernetes", "docker",
                "react", "angular", "vue", "javascript", "typescript", "node.js",
                "python", "java", "c#", "go", "rust", "kotlin", "swift",
                "devops", "ci/cd", "automation", "microservices", "api", "rest", "graphql",
                "database", "sql", "nosql", "mongodb", "postgresql", "redis",
                "security", "cybersecurity", "blockchain", "crypto", "web3",
                "mobile", "ios", "android", "flutter", "react native",
                "frontend", "backend", "fullstack", "framework", "library"
            };

            var words = text.ToLowerInvariant()
                .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(word => word.Length > 2);

            return words.Where(word => techKeywords.Contains(word)).Distinct().ToList();
        }
    }

    public class OpenAIResponse
    {
        public OpenAIChoice[]? Choices { get; set; }
    }

    public class OpenAIChoice
    {
        public OpenAIMessage? Message { get; set; }
    }

    public class OpenAIMessage
    {
        public string? Content { get; set; }
    }
}