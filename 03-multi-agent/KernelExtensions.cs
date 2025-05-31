using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

namespace WebSearchPluginExtensions;

/// <summary>
/// Extension methods for configuring the Kernel with plugins.
/// </summary>
public static class KernelExtensions
{
    /// <summary>
    /// Adds the WebSearchPlugin to the kernel with automatic fallback between local and public SearxNG instances.
    /// </summary>
    /// <param name="kernel">The kernel to add the plugin to</param>
    /// <param name="localSearxngUrl">Local SearxNG instance URL (optional)</param>
    /// <param name="publicSearxngUrl">Public SearxNG instance URL (default: https://searx.be)</param>
    /// <returns>The kernel instance for method chaining</returns>
    public static async Task<Kernel> AddWebSearchPluginAsync(
        this Kernel kernel,
        string? localSearxngUrl = null,
        string publicSearxngUrl = "https://searx.be")
    {
        var httpClient = kernel.Services.GetRequiredService<HttpClient>();
        
        Console.WriteLine("Testing SearxNG instances...");

        // Try local instance first if provided
        if (!string.IsNullOrEmpty(localSearxngUrl))
        {
            var localWebSearchPlugin = new WebSearchPlugin(httpClient, localSearxngUrl);
            Console.WriteLine($"Testing local SearxNG instance ({localSearxngUrl})...");
            
            try
            {
                var localResult = await localWebSearchPlugin.SearchWebAsync("test", 1);
                Console.WriteLine("✅ Local SearxNG instance is working!");
                kernel.Plugins.AddFromObject(localWebSearchPlugin, "WebSearch");
                return kernel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Local SearxNG instance failed: {ex.Message}");
                Console.WriteLine("Falling back to public SearxNG instance...");
            }
        }

        // Fall back to public instance
        var publicWebSearchPlugin = new WebSearchPlugin(httpClient, publicSearxngUrl);
        Console.WriteLine($"Testing public SearxNG instance ({publicSearxngUrl})...");
        
        try
        {
            var publicResult = await publicWebSearchPlugin.SearchWebAsync("test", 1);
            Console.WriteLine("✅ Public SearxNG instance is working!");
            kernel.Plugins.AddFromObject(publicWebSearchPlugin, "WebSearch");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Public SearxNG instance also failed: {ex.Message}");
            Console.WriteLine("Adding plugin anyway for demonstration...");
            kernel.Plugins.AddFromObject(publicWebSearchPlugin, "WebSearch");
        }

        return kernel;
    }

    /// <summary>
    /// Adds the WebSearchPlugin to the kernel with a specific SearxNG instance URL.
    /// </summary>
    /// <param name="kernel">The kernel to add the plugin to</param>
    /// <param name="searxngUrl">The SearxNG instance URL to use</param>
    /// <param name="testConnection">Whether to test the connection before adding (default: true)</param>
    /// <returns>The kernel instance for method chaining</returns>
    public static async Task<Kernel> AddWebSearchPluginAsync(
        this Kernel kernel,
        string searxngUrl,
        bool testConnection = true)
    {
        var httpClient = kernel.Services.GetRequiredService<HttpClient>();
        var webSearchPlugin = new WebSearchPlugin(httpClient, searxngUrl);

        if (testConnection)
        {
            Console.WriteLine($"Testing SearxNG instance ({searxngUrl})...");
            try
            {
                var result = await webSearchPlugin.SearchWebAsync("test", 1);
                Console.WriteLine("✅ SearxNG instance is working!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SearxNG instance test failed: {ex.Message}");
                Console.WriteLine("Adding plugin anyway...");
            }
        }

        kernel.Plugins.AddFromObject(webSearchPlugin, "WebSearch");
        return kernel;
    }

    /// <summary>
    /// Displays information about the WebSearchPlugin capabilities.
    /// </summary>
    /// <param name="kernel">The kernel containing the plugin</param>
    public static void DisplayWebSearchPluginInfo(this Kernel kernel)
    {
        Console.WriteLine("WebSearchPlugin is ready! Available functions:");
        Console.WriteLine("- SearchWebAsync: Search the web using SearxNG API");
        Console.WriteLine("- FetchPageAsMarkdownAsync: Fetch a specific web page as markdown");
        Console.WriteLine("- SearchAndFetchPagesAsync: Search and fetch multiple pages as markdown");
        Console.WriteLine();
    }
}
