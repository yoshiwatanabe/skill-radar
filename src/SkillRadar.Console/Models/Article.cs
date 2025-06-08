using System;
using System.Collections.Generic;

namespace SkillRadar.Console.Models
{
    public class Article
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty;
        public DateTime PublishedAt { get; set; }
        public List<string> TechTags { get; set; } = new List<string>();
        public int Score { get; set; }
        public float RelevanceScore { get; set; }
    }
}