# WebSearchPlugin for Semantic Kernel

This project demonstrates a comprehensive web search plugin that leverages the SearxNG API to search the web and convert web pages to markdown for LLM analysis.

## Features

The WebSearchPlugin provides three main functions:

### 1. SearchWebAsync
- Searches the web using SearxNG API
- Returns structured JSON results with titles, URLs, content snippets, and metadata
- Supports different search categories (general, news, images, videos)
- Configurable maximum number of results

### 2. FetchPageAsMarkdownAsync
- Fetches any web page and converts it to clean markdown
- Intelligently extracts main content (articles, main sections)
- Removes scripts, styles, and other non-content elements
- Supports content length limiting to prevent token overflow
- Handles various HTML structures and layouts

### 3. SearchAndFetchPagesAsync
- Combines search and page fetching in one operation
- Searches for results and automatically fetches the top pages
- Converts all pages to markdown for comprehensive analysis
- Perfect for research and information gathering tasks

## Dependencies

The plugin uses the following NuGet packages:
- `Microsoft.SemanticKernel` - Core Semantic Kernel functionality
- `Microsoft.Extensions.Http` - HTTP client services
- `HtmlAgilityPack` - HTML parsing and manipulation
- `ReverseMarkdown` - HTML to Markdown conversion

## Configuration

### SearxNG Instance
By default, the plugin uses `https://searx.be` as the SearxNG instance. You can configure a different instance:

```csharp
var webSearchPlugin = new WebSearchPlugin(httpClient, "https://your-searxng-instance.com");
```

### Popular SearxNG Instances
- `https://searx.be`
- `https://searx.tiekoetter.com`
- `https://search.sapti.me`
- `https://searx.fmac.xyz`

**Note:** Always respect the usage policies of the SearxNG instance you choose.

## Usage Examples

### Basic Setup
```csharp
// Create kernel with HTTP client
var builder = Kernel.CreateBuilder().AddOpenAIChatCompletion(modelId, apiKey);
builder.Services.AddHttpClient();
var kernel = builder.Build();

// Add WebSearchPlugin
var httpClient = kernel.Services.GetRequiredService<HttpClient>();
var webSearchPlugin = new WebSearchPlugin(httpClient);
kernel.Plugins.AddFromObject(webSearchPlugin, "WebSearch");
```

### Example Prompts
```csharp
// Research current events
string prompt1 = @"Use the web search to find recent information about 'artificial intelligence advancements 2024' 
and then provide a comprehensive summary of the key developments.";

// Analyze specific topics
string prompt2 = @"Search for information about 'sustainable energy solutions' and fetch the top 3 pages 
to provide a detailed analysis of current technologies and trends.";

// Get specific page content
string prompt3 = @"Fetch the content from https://example.com/article and summarize the main points.";
```

## Best Practices

1. **Rate Limiting**: Be mindful of rate limits when making multiple requests
2. **Content Length**: Use appropriate `maxLength` parameters to avoid token limits
3. **Error Handling**: The plugin includes comprehensive error handling for network issues
4. **Caching**: Consider implementing caching for frequently accessed content
5. **Respect robots.txt**: The plugin includes a User-Agent header for identification

## Technical Details

### Content Extraction
The plugin uses intelligent content extraction that:
- Tries multiple CSS selectors to find main content (`main`, `article`, `.content`, etc.)
- Falls back to body content if specific selectors aren't found
- Removes scripts, styles, and other non-content elements
- Preserves semantic structure in the markdown output

### Markdown Conversion
- Uses GitHub-flavored markdown for better compatibility
- Handles links, images, tables, and other HTML elements
- Cleans up excessive whitespace and empty elements
- Preserves formatting while improving readability

### Error Handling
- Network timeouts and connection errors
- Invalid URLs and malformed HTML
- Empty or missing content
- API rate limiting and service availability

## Security Considerations

- The plugin makes HTTP requests to external websites
- User-Agent headers are included for proper identification
- No sensitive data is logged or stored
- Consider implementing additional security measures for production use

## Troubleshooting

### Common Issues
1. **SearxNG Instance Down**: Try a different SearxNG instance
2. **No Search Results**: Check your query and try broader terms
3. **Page Fetch Errors**: Some sites may block automated requests
4. **Large Content**: Adjust `maxLength` parameters for better performance

### Error Messages
- "Query cannot be empty" - Provide a valid search query
- "Invalid URL format" - Ensure URLs are properly formatted
- "Search failed" - Check network connectivity and SearxNG instance status
- "Error fetching page" - The target website may be unavailable or blocking requests

## Contributing

To extend the plugin:
1. Add new search categories or filters
2. Implement additional content extraction strategies
3. Add support for different markup formats
4. Enhance error handling and retry logic
