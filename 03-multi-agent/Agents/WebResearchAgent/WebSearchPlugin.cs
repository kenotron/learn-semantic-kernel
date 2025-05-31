using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel;
using System.Text;

namespace MultiAgent.Agents.WebResearchAgent;

/// <summary>
/// A plugin that provides web search capabilities using SearxNG API and returns results as formatted Markdown.
/// </summary>
public class WebSearchPlugin
{
  private readonly HttpClient _httpClient;
  private readonly string _searxngBaseUrl;

  public WebSearchPlugin(HttpClient httpClient, string searxngBaseUrl = "https://searx.be")
  {
    _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    _searxngBaseUrl = searxngBaseUrl?.TrimEnd('/') ?? "https://searx.be";

    // Configure HttpClient with user agent
    if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
    {
      _httpClient.DefaultRequestHeaders.Add("User-Agent", "WebSearchPlugin/1.0");
    }
  }
  /// <summary>
  /// Searches the web using SearxNG API and returns search results as a formatted Markdown list.
  /// </summary>
  /// <param name="query">The search query</param>
  /// <param name="maxResults">Maximum number of results to return (default: 10)</param>
  /// <param name="categories">Search categories (default: general)</param>
  /// <returns>A Markdown formatted bulleted list of search results</returns>
  [KernelFunction, Description("Search the web using SearxNG API and return search results as a formatted Markdown list")]
  public async Task<string> SearchWebAsync(
      [Description("The search query")] string query,
      [Description("Maximum number of results to return")] int maxResults = 10,
      [Description("Search categories (e.g., general, news, images, videos)")] string categories = "general")  {
    if (string.IsNullOrWhiteSpace(query))
    {
      return "**Error:** Query cannot be empty";
    }

    try
    {
      var searchUrl = $"{_searxngBaseUrl}/search";
      var parameters = new Dictionary<string, string>
      {
        ["q"] = query,
        ["format"] = "json",
        ["categories"] = categories,
        ["pageno"] = "1"
      };

      var queryString = string.Join("&", parameters.Select(kvp => $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));
      var fullUrl = $"{searchUrl}?{queryString}";

      Console.WriteLine($"🔍 Searching: {fullUrl}");

      var response = await _httpClient.GetAsync(fullUrl);
      response.EnsureSuccessStatusCode();

      var content = await response.Content.ReadAsStringAsync();
      Console.WriteLine($"📄 Response length: {content.Length} characters");

      // Try to deserialize with proper data model
      SearxngSearchResult? searchResult = null;
      try
      {
        searchResult = JsonSerializer.Deserialize<SearxngSearchResult>(content);
        Console.WriteLine($"🔍 Deserialization successful. Query: '{searchResult?.Query}', Results: {searchResult?.Results?.Count ?? 0}");
      }
      catch (JsonException ex)
      {
        Console.WriteLine($"❌ JSON deserialization failed: {ex.Message}");
        return $"**Error:** JSON parsing failed: {ex.Message}";
      }

      if (searchResult?.Results == null || searchResult.Results.Count == 0)
      {
        Console.WriteLine("❌ No results found in response");
        return $"**No results found for query:** {query}";
      }

      Console.WriteLine($"✅ Found {searchResult.Results.Count} results");

      // Format results as Markdown bulleted list
      var markdownBuilder = new StringBuilder();
      markdownBuilder.AppendLine($"# Web Search Results for: {query}");
      markdownBuilder.AppendLine();
      markdownBuilder.AppendLine($"**Found {Math.Min(searchResult.Results.Count, maxResults)} results:**");
      markdownBuilder.AppendLine();

      var limitedResults = searchResult.Results.Take(maxResults);
      
      foreach (var result in limitedResults)
      {
        markdownBuilder.AppendLine($"• **[{result.Title}]({result.Url})**");
        
        if (!string.IsNullOrWhiteSpace(result.Content))
        {
          // Clean and truncate content for preview
          var preview = CleanTextContent(result.Content);
          if (preview.Length > 200)
          {
            preview = preview.Substring(0, 200) + "...";
          }
          markdownBuilder.AppendLine($"  {preview}");
        }
        
        if (!string.IsNullOrWhiteSpace(result.PublishedDate))
        {
          markdownBuilder.AppendLine($"  *Published: {result.PublishedDate}*");
        }
        
        if (!string.IsNullOrWhiteSpace(result.Engine))
        {
          markdownBuilder.AppendLine($"  *Source: {result.Engine}*");
        }
        
        markdownBuilder.AppendLine();
      }

      return markdownBuilder.ToString();
    }
    catch (Exception ex)
    {
      return $"**Error:** Search failed: {ex.Message}";
    }  }

  private string CleanTextContent(string content)
  {
    if (string.IsNullOrEmpty(content))
      return string.Empty;

    // Remove HTML tags if any
    content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]*>", "");
    
    // Replace multiple whitespace with single space
    content = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ");
    
    // Remove newlines and tabs
    content = content.Replace("\n", " ").Replace("\r", " ").Replace("\t", " ");
    
    return content.Trim();
  }
}

// Data classes for SearxNG API response
public class SearxngSearchResult
{
  [JsonPropertyName("query")]
  public string Query { get; set; } = string.Empty;

  [JsonPropertyName("number_of_results")]
  public int NumberOfResults { get; set; }

  [JsonPropertyName("results")]
  public List<SearchResult> Results { get; set; } = new();
}

public class SearchResult
{
  [JsonPropertyName("title")]
  public string Title { get; set; } = string.Empty;

  [JsonPropertyName("url")]
  public string Url { get; set; } = string.Empty;

  [JsonPropertyName("content")]
  public string Content { get; set; } = string.Empty;

  [JsonPropertyName("publishedDate")]
  public string? PublishedDate { get; set; }

  [JsonPropertyName("engine")]
  public string Engine { get; set; } = string.Empty;

  [JsonPropertyName("engines")]
  public List<string>? Engines { get; set; }

  [JsonPropertyName("score")]
  public double? Score { get; set; }

  [JsonPropertyName("category")]
  public string? Category { get; set; }
}

