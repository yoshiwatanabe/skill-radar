using System.Collections.Generic;

namespace SkillRadar.Console.Models
{
    public class UserProfile
    {
        public List<string> Skills { get; set; } = new List<string>();
        public List<string> Interests { get; set; } = new List<string>();
        public string CareerStage { get; set; } = string.Empty;
        public List<string> LearningGoals { get; set; } = new List<string>();
    }

    public class DataSource
    {
        public bool Enabled { get; set; }
        public string Priority { get; set; } = string.Empty;
        public List<string>? Subreddits { get; set; }
        public List<string>? Categories { get; set; }
    }

    public class ReportSettings
    {
        public int MaxArticlesPerSource { get; set; }
        public int TopTrendsCount { get; set; }
        public int MustReadCount { get; set; }
        public List<string> OutputFormats { get; set; } = new List<string>();
    }
}