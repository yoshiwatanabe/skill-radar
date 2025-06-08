using System;
using System.Collections.Generic;

namespace SkillRadar.Console.Models
{
    public class TrendReport
    {
        public DateTime WeekStart { get; set; }
        public DateTime WeekEnd { get; set; }
        public List<TrendingTopic> TopTrends { get; set; } = new List<TrendingTopic>();
        public List<Article> MustReadArticles { get; set; } = new List<Article>();
        public List<string> LearningRecommendations { get; set; } = new List<string>();
        public Dictionary<string, int> TechKeywordFrequency { get; set; } = new Dictionary<string, int>();
        public string WeeklySummary { get; set; } = string.Empty;
    }

    public class TrendingTopic
    {
        public string Name { get; set; } = string.Empty;
        public int MentionCount { get; set; }
        public string KeyInsight { get; set; } = string.Empty;
        public string LearningRecommendation { get; set; } = string.Empty;
        public List<Article> RelatedArticles { get; set; } = new List<Article>();
    }
}