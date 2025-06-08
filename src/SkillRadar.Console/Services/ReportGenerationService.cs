using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SkillRadar.Console.Models;

namespace SkillRadar.Console.Services
{
    public class ReportGenerationService
    {
        public async Task GenerateConsoleReportAsync(TrendReport report)
        {
            var output = GenerateReportContent(report);
            Console.WriteLine(output);
        }

        public async Task GenerateFileReportAsync(TrendReport report, string filePath, string format = "markdown")
        {
            var content = format.ToLowerInvariant() switch
            {
                "html" => GenerateHtmlReport(report),
                "json" => GenerateJsonReport(report),
                _ => GenerateMarkdownReport(report)
            };

            await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);
            Console.WriteLine($"Report saved to: {filePath}");
        }

        private string GenerateReportContent(TrendReport report)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine($" Weekly Tech Trend Report ({report.WeekStart:MMM d} - {report.WeekEnd:MMM d, yyyy})");
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine();

            if (!string.IsNullOrEmpty(report.WeeklySummary))
            {
                sb.AppendLine("📋 WEEKLY SUMMARY");
                sb.AppendLine("-".PadRight(40, '-'));
                sb.AppendLine(WrapText(report.WeeklySummary, 70));
                sb.AppendLine();
            }

            if (report.TopTrends.Any())
            {
                sb.AppendLine("🔥 TOP TRENDING TECHNOLOGIES");
                sb.AppendLine("-".PadRight(40, '-'));
                
                for (int i = 0; i < report.TopTrends.Count; i++)
                {
                    var trend = report.TopTrends[i];
                    sb.AppendLine($"{i + 1}. **{trend.Name}** ({trend.MentionCount} mentions)");
                    
                    if (!string.IsNullOrEmpty(trend.KeyInsight))
                    {
                        sb.AppendLine($"   💡 Key insight: {trend.KeyInsight}");
                    }
                    
                    if (!string.IsNullOrEmpty(trend.LearningRecommendation))
                    {
                        sb.AppendLine($"   📚 Learning recommendation: {trend.LearningRecommendation}");
                    }
                    
                    sb.AppendLine();
                }
            }

            if (report.MustReadArticles.Any())
            {
                sb.AppendLine("📚 MUST-READ ARTICLES");
                sb.AppendLine("-".PadRight(40, '-'));
                
                for (int i = 0; i < report.MustReadArticles.Count; i++)
                {
                    var article = report.MustReadArticles[i];
                    sb.AppendLine($"{i + 1}. \"{article.Title}\" - {(article.RelevanceScore * 100):F0}% relevance");
                    sb.AppendLine($"   📰 Source: {article.Source}");
                    sb.AppendLine($"   🔗 URL: {article.Url}");
                    
                    if (!string.IsNullOrEmpty(article.Summary))
                    {
                        var summary = article.Summary.Length > 150 
                            ? article.Summary.Substring(0, 150) + "..." 
                            : article.Summary;
                        sb.AppendLine($"   📝 Summary: {summary}");
                    }
                    
                    if (article.TechTags.Any())
                    {
                        sb.AppendLine($"   🏷️  Tags: {string.Join(", ", article.TechTags.Take(5))}");
                    }
                    
                    sb.AppendLine();
                }
            }

            if (report.LearningRecommendations.Any())
            {
                sb.AppendLine("🎯 THIS WEEK'S LEARNING FOCUS");
                sb.AppendLine("-".PadRight(40, '-'));
                
                foreach (var recommendation in report.LearningRecommendations)
                {
                    sb.AppendLine($"• {recommendation}");
                }
                sb.AppendLine();
            }

            if (report.TechKeywordFrequency.Any())
            {
                sb.AppendLine("📊 TECHNOLOGY BUZZ WORDS");
                sb.AppendLine("-".PadRight(40, '-'));
                
                var topKeywords = report.TechKeywordFrequency
                    .OrderByDescending(kv => kv.Value)
                    .Take(10)
                    .ToList();
                
                foreach (var keyword in topKeywords)
                {
                    var bar = "█".PadRight((int)Math.Ceiling(keyword.Value / 5.0), '█');
                    sb.AppendLine($"{keyword.Key.PadRight(20)} {bar} ({keyword.Value})");
                }
                sb.AppendLine();
            }

            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine($"Generated on {DateTime.Now:yyyy-MM-dd HH:mm} by SkillRadar");
            sb.AppendLine("=".PadRight(60, '='));

            return sb.ToString();
        }

        private string GenerateMarkdownReport(TrendReport report)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine($"# Weekly Tech Trend Report");
            sb.AppendLine($"**Period:** {report.WeekStart:MMM d} - {report.WeekEnd:MMM d, yyyy}");
            sb.AppendLine();

            if (!string.IsNullOrEmpty(report.WeeklySummary))
            {
                sb.AppendLine("## 📋 Weekly Summary");
                sb.AppendLine();
                sb.AppendLine(report.WeeklySummary);
                sb.AppendLine();
            }

            if (report.TopTrends.Any())
            {
                sb.AppendLine("## 🔥 Top Trending Technologies");
                sb.AppendLine();
                
                for (int i = 0; i < report.TopTrends.Count; i++)
                {
                    var trend = report.TopTrends[i];
                    sb.AppendLine($"### {i + 1}. {trend.Name} ({trend.MentionCount} mentions)");
                    
                    if (!string.IsNullOrEmpty(trend.KeyInsight))
                    {
                        sb.AppendLine($"**Key insight:** {trend.KeyInsight}");
                        sb.AppendLine();
                    }
                    
                    if (!string.IsNullOrEmpty(trend.LearningRecommendation))
                    {
                        sb.AppendLine($"**Learning recommendation:** {trend.LearningRecommendation}");
                        sb.AppendLine();
                    }
                }
            }

            if (report.MustReadArticles.Any())
            {
                sb.AppendLine("## 📚 Must-Read Articles");
                sb.AppendLine();
                
                for (int i = 0; i < report.MustReadArticles.Count; i++)
                {
                    var article = report.MustReadArticles[i];
                    sb.AppendLine($"### {i + 1}. [{article.Title}]({article.Url})");
                    sb.AppendLine($"**Source:** {article.Source} | **Relevance:** {(article.RelevanceScore * 100):F0}%");
                    
                    if (!string.IsNullOrEmpty(article.Summary))
                    {
                        sb.AppendLine();
                        sb.AppendLine(article.Summary);
                    }
                    
                    if (article.TechTags.Any())
                    {
                        sb.AppendLine();
                        sb.AppendLine($"**Tags:** {string.Join(", ", article.TechTags.Take(5))}");
                    }
                    
                    sb.AppendLine();
                }
            }

            if (report.LearningRecommendations.Any())
            {
                sb.AppendLine("## 🎯 This Week's Learning Focus");
                sb.AppendLine();
                
                foreach (var recommendation in report.LearningRecommendations)
                {
                    sb.AppendLine($"- {recommendation}");
                }
                sb.AppendLine();
            }

            if (report.TechKeywordFrequency.Any())
            {
                sb.AppendLine("## 📊 Technology Buzz Words");
                sb.AppendLine();
                sb.AppendLine("| Technology | Mentions |");
                sb.AppendLine("|------------|----------|");
                
                var topKeywords = report.TechKeywordFrequency
                    .OrderByDescending(kv => kv.Value)
                    .Take(10)
                    .ToList();
                
                foreach (var keyword in topKeywords)
                {
                    sb.AppendLine($"| {keyword.Key} | {keyword.Value} |");
                }
                sb.AppendLine();
            }

            sb.AppendLine("---");
            sb.AppendLine($"*Generated on {DateTime.Now:yyyy-MM-dd HH:mm} by SkillRadar*");

            return sb.ToString();
        }

        private string GenerateHtmlReport(TrendReport report)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("    <title>Weekly Tech Trend Report</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; margin: 40px; line-height: 1.6; color: #333; }");
            sb.AppendLine("        .header { border-bottom: 3px solid #007acc; padding-bottom: 20px; margin-bottom: 30px; }");
            sb.AppendLine("        h1 { color: #007acc; margin: 0; }");
            sb.AppendLine("        .period { color: #666; font-size: 1.1em; margin-top: 10px; }");
            sb.AppendLine("        .section { margin-bottom: 40px; }");
            sb.AppendLine("        h2 { color: #333; border-left: 4px solid #007acc; padding-left: 15px; }");
            sb.AppendLine("        .trend { background: #f8f9fa; padding: 20px; margin-bottom: 20px; border-radius: 8px; border-left: 4px solid #28a745; }");
            sb.AppendLine("        .article { background: #f8f9fa; padding: 20px; margin-bottom: 20px; border-radius: 8px; border-left: 4px solid #17a2b8; }");
            sb.AppendLine("        .article-title { color: #007acc; text-decoration: none; font-weight: bold; }");
            sb.AppendLine("        .article-title:hover { text-decoration: underline; }");
            sb.AppendLine("        .meta { color: #666; font-size: 0.9em; margin-top: 10px; }");
            sb.AppendLine("        .tags { margin-top: 10px; }");
            sb.AppendLine("        .tag { background: #e9ecef; padding: 3px 8px; border-radius: 3px; font-size: 0.8em; margin-right: 5px; }");
            sb.AppendLine("        .keyword-chart { margin-top: 20px; }");
            sb.AppendLine("        .keyword-bar { display: flex; align-items: center; margin-bottom: 8px; }");
            sb.AppendLine("        .keyword-name { width: 120px; font-weight: bold; }");
            sb.AppendLine("        .keyword-visual { background: #007acc; height: 20px; margin-left: 10px; border-radius: 2px; }");
            sb.AppendLine("        .recommendation { background: #fff3cd; padding: 15px; margin-bottom: 10px; border-radius: 5px; border-left: 4px solid #ffc107; }");
            sb.AppendLine("        .footer { margin-top: 50px; padding-top: 20px; border-top: 1px solid #dee2e6; color: #666; text-align: center; }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            
            sb.AppendLine("    <div class=\"header\">");
            sb.AppendLine("        <h1>📊 Weekly Tech Trend Report</h1>");
            sb.AppendLine($"        <div class=\"period\">{report.WeekStart:MMM d} - {report.WeekEnd:MMM d, yyyy}</div>");
            sb.AppendLine("    </div>");

            if (!string.IsNullOrEmpty(report.WeeklySummary))
            {
                sb.AppendLine("    <div class=\"section\">");
                sb.AppendLine("        <h2>📋 Weekly Summary</h2>");
                sb.AppendLine($"        <p>{report.WeeklySummary}</p>");
                sb.AppendLine("    </div>");
            }

            if (report.TopTrends.Any())
            {
                sb.AppendLine("    <div class=\"section\">");
                sb.AppendLine("        <h2>🔥 Top Trending Technologies</h2>");
                
                for (int i = 0; i < report.TopTrends.Count; i++)
                {
                    var trend = report.TopTrends[i];
                    sb.AppendLine("        <div class=\"trend\">");
                    sb.AppendLine($"            <h3>{i + 1}. {trend.Name} <span style=\"color: #666;\">({trend.MentionCount} mentions)</span></h3>");
                    
                    if (!string.IsNullOrEmpty(trend.KeyInsight))
                    {
                        sb.AppendLine($"            <p><strong>💡 Key insight:</strong> {trend.KeyInsight}</p>");
                    }
                    
                    if (!string.IsNullOrEmpty(trend.LearningRecommendation))
                    {
                        sb.AppendLine($"            <p><strong>📚 Learning recommendation:</strong> {trend.LearningRecommendation}</p>");
                    }
                    
                    sb.AppendLine("        </div>");
                }
                sb.AppendLine("    </div>");
            }

            if (report.MustReadArticles.Any())
            {
                sb.AppendLine("    <div class=\"section\">");
                sb.AppendLine("        <h2>📚 Must-Read Articles</h2>");
                
                for (int i = 0; i < report.MustReadArticles.Count; i++)
                {
                    var article = report.MustReadArticles[i];
                    sb.AppendLine("        <div class=\"article\">");
                    sb.AppendLine($"            <h3><a href=\"{article.Url}\" class=\"article-title\" target=\"_blank\">{i + 1}. {article.Title}</a></h3>");
                    sb.AppendLine($"            <div class=\"meta\">Source: {article.Source} | Relevance: {(article.RelevanceScore * 100):F0}%</div>");
                    
                    if (!string.IsNullOrEmpty(article.Summary))
                    {
                        sb.AppendLine($"            <p>{article.Summary}</p>");
                    }
                    
                    if (article.TechTags.Any())
                    {
                        sb.AppendLine("            <div class=\"tags\">");
                        foreach (var tag in article.TechTags.Take(5))
                        {
                            sb.AppendLine($"                <span class=\"tag\">{tag}</span>");
                        }
                        sb.AppendLine("            </div>");
                    }
                    
                    sb.AppendLine("        </div>");
                }
                sb.AppendLine("    </div>");
            }

            if (report.LearningRecommendations.Any())
            {
                sb.AppendLine("    <div class=\"section\">");
                sb.AppendLine("        <h2>🎯 This Week's Learning Focus</h2>");
                
                foreach (var recommendation in report.LearningRecommendations)
                {
                    sb.AppendLine($"        <div class=\"recommendation\">{recommendation}</div>");
                }
                sb.AppendLine("    </div>");
            }

            sb.AppendLine("    <div class=\"footer\">");
            sb.AppendLine($"        Generated on {DateTime.Now:yyyy-MM-dd HH:mm} by SkillRadar");
            sb.AppendLine("    </div>");
            
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            return sb.ToString();
        }

        private string GenerateJsonReport(TrendReport report)
        {
            return System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        }

        private string WrapText(string text, int maxWidth)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= maxWidth)
                return text;

            var words = text.Split(' ');
            var lines = new System.Collections.Generic.List<string>();
            var currentLine = "";

            foreach (var word in words)
            {
                if ((currentLine + " " + word).Length <= maxWidth)
                {
                    currentLine += (currentLine.Length == 0 ? "" : " ") + word;
                }
                else
                {
                    if (currentLine.Length > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = word;
                    }
                    else
                    {
                        lines.Add(word);
                    }
                }
            }

            if (currentLine.Length > 0)
                lines.Add(currentLine);

            return string.Join("\n", lines);
        }
    }
}