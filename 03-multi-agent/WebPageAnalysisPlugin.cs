using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using HtmlAgilityPack;
using ReverseMarkdown;
using System.Text;
using System.Text.RegularExpressions;

namespace WebResearchAgents;

/// <summary>
/// A plugin that analyzes web pages, extracts content and related links, and assesses their research value.
/// </summary>
public class WebPageAnalysisPlugin
{
    private readonly HttpClient _httpClient;
    private readonly ReverseMarkdown.Converter _markdownConverter;

    public WebPageAnalysisPlugin(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

        // Configure the markdown converter for better output
        var config = new ReverseMarkdown.Config
        {
            UnknownTags = ReverseMarkdown.Config.UnknownTagsOption.PassThrough,
            GithubFlavored = true,
            RemoveComments = true,
            SmartHrefHandling = true
        };
        _markdownConverter = new ReverseMarkdown.Converter(config);

        // Configure HttpClient with user agent
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "WebPageAnalysisPlugin/1.0");
        }
    }

    /// <summary>
    /// Analyzes a web page by extracting its content, finding related links, and assessing research value.
    /// </summary>
    /// <param name="url">The URL of the page to analyze</param>
    /// <param name="researchObjective">The research objective or topic to assess relevance against</param>
    /// <param name="maxContentLength">Maximum length of content to return (default: 8000)</param>
    /// <param name="maxLinks">Maximum number of related links to extract (default: 10)</param>
    /// <returns>A comprehensive analysis of the page including content, related links, and research value assessment</returns>
    [KernelFunction, Description("Analyze a web page to extract content, find related links, and assess research value for a given objective")]
    public async Task<string> AnalyzePageAsync(
        [Description("The URL of the page to analyze")] string url,
        [Description("The research objective or topic to assess relevance against")] string researchObjective,
        [Description("Maximum length of content to return")] int maxContentLength = 8000,
        [Description("Maximum number of related links to extract")] int maxLinks = 10)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "**Error:** URL cannot be empty";
        }

        if (string.IsNullOrWhiteSpace(researchObjective))
        {
            return "**Error:** Research objective cannot be empty";
        }

        try
        {
            Console.WriteLine($"🔍 Analyzing page: {url}");
            Console.WriteLine($"📋 Research objective: {researchObjective}");

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var html = await response.Content.ReadAsStringAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract page information
            var title = GetPageTitle(doc);
            var mainContent = ExtractMainContent(doc);
            var markdown = _markdownConverter.Convert(mainContent);
            var cleanedMarkdown = CleanMarkdown(markdown);

            // Limit content length
            if (cleanedMarkdown.Length > maxContentLength)
            {
                cleanedMarkdown = cleanedMarkdown.Substring(0, maxContentLength) + "\n\n[Content truncated...]";
            }

            // Extract related links
            var relatedLinks = ExtractRelatedLinks(doc, url, maxLinks);

            // Assess research value
            var researchValue = AssessResearchValue(title, cleanedMarkdown, relatedLinks, researchObjective);

            // Build comprehensive analysis report
            var analysisBuilder = new StringBuilder();
            analysisBuilder.AppendLine($"# Web Page Analysis: {title}");
            analysisBuilder.AppendLine($"**URL:** {url}");
            analysisBuilder.AppendLine($"**Research Objective:** {researchObjective}");
            analysisBuilder.AppendLine();

            // Research Value Assessment
            analysisBuilder.AppendLine("## 🎯 Research Value Assessment");
            analysisBuilder.AppendLine($"**Overall Value:** {researchValue.OverallValue}");
            analysisBuilder.AppendLine($"**Relevance Score:** {researchValue.RelevanceScore}/10");
            analysisBuilder.AppendLine($"**Key Insights:** {researchValue.KeyInsights}");
            if (!string.IsNullOrWhiteSpace(researchValue.Recommendations))
            {
                analysisBuilder.AppendLine($"**Recommendations:** {researchValue.Recommendations}");
            }
            analysisBuilder.AppendLine();

            // Page Content
            analysisBuilder.AppendLine("## 📄 Page Content");
            analysisBuilder.AppendLine(cleanedMarkdown);
            analysisBuilder.AppendLine();

            // Related Links
            if (relatedLinks.Any())
            {
                analysisBuilder.AppendLine("## 🔗 Related Links");
                analysisBuilder.AppendLine($"**Found {relatedLinks.Count} potentially relevant links:**");
                analysisBuilder.AppendLine();

                foreach (var link in relatedLinks)
                {
                    analysisBuilder.AppendLine($"• **[{link.Title}]({link.Url})**");
                    if (!string.IsNullOrWhiteSpace(link.Description))
                    {
                        analysisBuilder.AppendLine($"  {link.Description}");
                    }
                    analysisBuilder.AppendLine($"  *Relevance: {link.RelevanceAssessment}*");
                    analysisBuilder.AppendLine();
                }
            }

            Console.WriteLine($"✅ Successfully analyzed page: {title} ({cleanedMarkdown.Length} chars, {relatedLinks.Count} links)");
            return analysisBuilder.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error analyzing page {url}: {ex.Message}");
            return $"**Error analyzing page {url}:** {ex.Message}";
        }
    }

    private string GetPageTitle(HtmlDocument doc)
    {
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        return titleNode?.InnerText?.Trim() ?? "Untitled Page";
    }

    private string ExtractMainContent(HtmlDocument doc)
    {
        // Try to find main content using common selectors
        var selectors = new[]
        {
            "main",
            "article",
            "[role='main']",
            ".main-content",
            ".content",
            ".post-content",
            ".entry-content",
            "#main",
            "#content",
            ".article-body",
            ".story-body"
        };

        foreach (var selector in selectors)
        {
            var element = doc.DocumentNode.SelectSingleNode($"//{selector}");
            if (element != null && !string.IsNullOrWhiteSpace(element.InnerText) && element.InnerText.Length > 100)
            {
                return element.OuterHtml;
            }
        }

        // Fall back to body content, removing navigation and footer
        var body = doc.DocumentNode.SelectSingleNode("//body");
        if (body != null)
        {
            // Remove common non-content elements
            var elementsToRemove = body.SelectNodes(".//nav | .//footer | .//header | .//aside | .//*[@class='sidebar'] | .//*[@class='navigation']");
            if (elementsToRemove != null)
            {
                foreach (var element in elementsToRemove)
                {
                    element.Remove();
                }
            }
            return body.OuterHtml;
        }

        return doc.DocumentNode.OuterHtml;
    }

    private string CleanMarkdown(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
            return string.Empty;

        // Remove excessive newlines
        markdown = Regex.Replace(markdown, @"\n{3,}", "\n\n");

        // Remove empty links
        markdown = Regex.Replace(markdown, @"\[\]\(\)", "");

        // Clean up extra spaces
        markdown = Regex.Replace(markdown, @"[ ]{2,}", " ");

        // Remove common unwanted elements
        markdown = Regex.Replace(markdown, @"\[Skip to.*?\]", "", RegexOptions.IgnoreCase);
        markdown = Regex.Replace(markdown, @"\[Advertisement\]", "", RegexOptions.IgnoreCase);

        return markdown.Trim();
    }

    private List<RelatedLink> ExtractRelatedLinks(HtmlDocument doc, string baseUrl, int maxLinks)
    {
        var links = new List<RelatedLink>();
        var baseUri = new Uri(baseUrl);

        // Find all links
        var linkNodes = doc.DocumentNode.SelectNodes("//a[@href]");
        if (linkNodes == null) return links;

        var processedUrls = new HashSet<string>();

        foreach (var linkNode in linkNodes)
        {
            var href = linkNode.GetAttributeValue("href", "");
            if (string.IsNullOrWhiteSpace(href)) continue;

            // Convert relative URLs to absolute
            if (!Uri.TryCreate(baseUri, href, out var absoluteUri)) continue;
            var absoluteUrl = absoluteUri.ToString();

            // Skip if already processed
            if (processedUrls.Contains(absoluteUrl)) continue;
            processedUrls.Add(absoluteUrl);

            // Skip certain types of links
            if (ShouldSkipLink(absoluteUrl, baseUrl)) continue;

            var linkText = linkNode.InnerText?.Trim() ?? "";
            var title = linkNode.GetAttributeValue("title", "") ?? linkText;

            if (!string.IsNullOrWhiteSpace(linkText) && linkText.Length > 3)
            {
                links.Add(new RelatedLink
                {
                    Url = absoluteUrl,
                    Title = string.IsNullOrWhiteSpace(title) ? linkText : title,
                    Description = linkText != title ? linkText : "",
                    RelevanceAssessment = AssessLinkRelevance(title, linkText, absoluteUrl)
                });
            }

            if (links.Count >= maxLinks) break;
        }

        // Sort by relevance and return top results
        return links.OrderByDescending(l => GetRelevanceScore(l.RelevanceAssessment)).ToList();
    }

    private bool ShouldSkipLink(string url, string baseUrl)
    {
        var lowerUrl = url.ToLower();
        
        // Skip same page links
        if (url.StartsWith(baseUrl + "#")) return true;
        
        // Skip non-content links
        var skipPatterns = new[]
        {
            "javascript:", "mailto:", "tel:", "ftp:",
            "/login", "/signup", "/register", "/subscribe",
            "/privacy", "/terms", "/cookie", "/contact",
            ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx",
            ".zip", ".rar", ".tar", ".gz",
            "/feed", "/rss", "/xml",
            "facebook.com", "twitter.com", "linkedin.com", "instagram.com",
            "youtube.com", "tiktok.com"
        };

        return skipPatterns.Any(pattern => lowerUrl.Contains(pattern));
    }

    private string AssessLinkRelevance(string title, string linkText, string url)
    {
        var text = $"{title} {linkText}".ToLower();
        
        // High relevance indicators
        if (text.Contains("research") || text.Contains("study") || text.Contains("analysis") || 
            text.Contains("report") || text.Contains("whitepaper") || text.Contains("findings"))
        {
            return "High - Contains research-related content";
        }

        // Medium relevance indicators
        if (text.Contains("article") || text.Contains("news") || text.Contains("blog") ||
            text.Contains("guide") || text.Contains("tutorial") || text.Contains("overview"))
        {
            return "Medium - General informational content";
        }

        // Check URL for academic or authoritative sources
        var lowerUrl = url.ToLower();
        if (lowerUrl.Contains(".edu") || lowerUrl.Contains(".gov") || lowerUrl.Contains(".org") ||
            lowerUrl.Contains("scholar") || lowerUrl.Contains("pubmed") || lowerUrl.Contains("arxiv"))
        {
            return "High - Authoritative/academic source";
        }

        return "Low - General link";
    }

    private int GetRelevanceScore(string assessment)
    {
        if (assessment.StartsWith("High")) return 3;
        if (assessment.StartsWith("Medium")) return 2;
        return 1;
    }

    private ResearchValueAssessment AssessResearchValue(string title, string content, List<RelatedLink> links, string objective)
    {
        var assessment = new ResearchValueAssessment();
        var objectiveLower = objective.ToLower();
        var titleLower = title.ToLower();
        var contentLower = content.ToLower();

        // Calculate relevance score based on keyword matching
        var objectiveKeywords = ExtractKeywords(objectiveLower);
        int matches = 0;
        int totalKeywords = objectiveKeywords.Count;

        foreach (var keyword in objectiveKeywords)
        {
            if (titleLower.Contains(keyword) || contentLower.Contains(keyword))
            {
                matches++;
            }
        }

        assessment.RelevanceScore = totalKeywords > 0 ? (int)Math.Round((double)matches / totalKeywords * 10) : 5;

        // Assess overall value
        if (assessment.RelevanceScore >= 8)
        {
            assessment.OverallValue = "Very High";
            assessment.KeyInsights = "Highly relevant to research objective with strong keyword alignment";
            assessment.Recommendations = "Excellent source - analyze thoroughly and follow up on related links";
        }
        else if (assessment.RelevanceScore >= 6)
        {
            assessment.OverallValue = "High";
            assessment.KeyInsights = "Good relevance to research objective";
            assessment.Recommendations = "Valuable source - extract key information and check related links";
        }
        else if (assessment.RelevanceScore >= 4)
        {
            assessment.OverallValue = "Medium";
            assessment.KeyInsights = "Some relevance to research objective";
            assessment.Recommendations = "Moderately useful - scan for specific insights";
        }
        else
        {
            assessment.OverallValue = "Low";
            assessment.KeyInsights = "Limited relevance to research objective";
            assessment.Recommendations = "May not be worth deep analysis unless looking for background context";
        }

        // Boost score if page has many high-quality related links
        var highQualityLinks = links.Count(l => l.RelevanceAssessment.StartsWith("High"));
        if (highQualityLinks >= 3)
        {
            assessment.RelevanceScore = Math.Min(10, assessment.RelevanceScore + 1);
            assessment.KeyInsights += " | Rich in high-quality related resources";
        }

        return assessment;
    }

    private List<string> ExtractKeywords(string text)
    {
        // Simple keyword extraction - split on common delimiters and filter
        var words = text.Split(new char[] { ' ', ',', '.', '!', '?', ';', ':', '-', '_' }, 
                               StringSplitOptions.RemoveEmptyEntries)
                        .Where(w => w.Length > 3) // Only words longer than 3 characters
                        .Select(w => w.ToLower())
                        .Distinct()
                        .ToList();

        // Remove common stop words
        var stopWords = new HashSet<string> { "the", "and", "for", "are", "but", "not", "you", "all", "can", "had", "her", "was", "one", "our", "out", "day", "get", "has", "him", "his", "how", "its", "may", "new", "now", "old", "see", "two", "who", "boy", "did", "does", "lets", "put", "say", "she", "too", "use" };
        
        return words.Where(w => !stopWords.Contains(w)).ToList();
    }
}

// Supporting data classes
public class RelatedLink
{
    public string Url { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RelevanceAssessment { get; set; } = string.Empty;
}

public class ResearchValueAssessment
{
    public string OverallValue { get; set; } = string.Empty;
    public int RelevanceScore { get; set; }
    public string KeyInsights { get; set; } = string.Empty;
    public string Recommendations { get; set; } = string.Empty;
}
