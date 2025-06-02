using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

public class MultiPerspectiveAgent
{
  private readonly IChatCompletionService _chatService;
  private readonly Kernel _kernel;
  private readonly List<ExpertPerspective> _perspectives;
  private readonly OpenAIPromptExecutionSettings _toolCallingSettings;

  public MultiPerspectiveAgent(Kernel kernel)
  {
    _kernel = kernel;
    _chatService = kernel.GetRequiredService<IChatCompletionService>();
    _perspectives = InitializePerspectives();

    // Configure tool calling settings
    _toolCallingSettings = new OpenAIPromptExecutionSettings
    {
      MaxTokens = 800,
      Temperature = 0.7,
      ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
    };
  }

  private List<ExpertPerspective> InitializePerspectives()
  {
    return new List<ExpertPerspective>
        {
            new ExpertPerspective
            {
                Name = "technical_expert",
                SystemPrompt = "You are a technical expert focused on implementation details, performance, scalability, and best practices. You can use available tools to gather technical data, check system status, or analyze performance metrics.",
                Keywords = new[] { "implementation", "performance", "scalability", "architecture", "code", "technical" },
                CanUseTool = (toolName) => toolName.Contains("system") || toolName.Contains("performance") || toolName.Contains("database")
            },
            new ExpertPerspective
            {
                Name = "business_advisor",
                SystemPrompt = "You are a business strategist focused on ROI, cost-benefit analysis, and business impact. You can use tools to gather market data, financial information, or business metrics.",
                Keywords = new[] { "cost", "business", "ROI", "strategy", "budget", "value" },
                CanUseTool = (toolName) => toolName.Contains("finance") || toolName.Contains("market") || toolName.Contains("analytics")
            },
            new ExpertPerspective
            {
                Name = "user_experience_advocate",
                SystemPrompt = "You are a UX advocate focused on user impact, usability, and user satisfaction. You can use tools to gather user feedback, analyze usage patterns, or check user interface metrics.",
                Keywords = new[] { "user", "experience", "usability", "interface", "satisfaction" },
                CanUseTool = (toolName) => toolName.Contains("user") || toolName.Contains("feedback") || toolName.Contains("analytics")
            },
            new ExpertPerspective
            {
                Name = "systems_architect",
                SystemPrompt = "You are a systems architect focused on overall design, integration, and long-term maintainability. You can use tools to check system health, analyze architecture, or gather infrastructure data.",
                Keywords = new[] { "architecture", "design", "integration", "scalability", "maintainability" },
                CanUseTool = (toolName) => toolName.Contains("system") || toolName.Contains("infrastructure") || toolName.Contains("monitoring")
            },
            new ExpertPerspective
            {
                Name = "data_analyst",
                SystemPrompt = "You are a data analyst focused on gathering, analyzing, and interpreting data to support decision-making. You excel at using tools to retrieve information and perform analysis.",
                Keywords = new[] { "data", "analysis", "metrics", "research", "information" },
                CanUseTool = (toolName) => true // Data analyst can use most tools for research
            }
        };
  }

  public async Task<ChatResponse> ProcessUserMessageAsync(string userMessage, ChatHistory chatHistory)
  {
    // Step 1: Determine if tools are needed and select relevant perspectives
    var needsTools = await DetermineIfToolsNeededAsync(userMessage, chatHistory);
    var relevantPerspectives = SelectRelevantPerspectives(userMessage, needsTools);

    // Step 2: Generate internal reasoning from each perspective (with tool access)
    var internalReasoningTasks = relevantPerspectives.Select(perspective =>
        GeneratePerspectiveThoughtsWithToolsAsync(perspective, userMessage, chatHistory, needsTools));

    var perspectiveResults = await Task.WhenAll(internalReasoningTasks);

    // Step 3: Create the internal dialog structure
    var internalDialog = CreateInternalDialogWithToolResults(perspectiveResults);

    // Step 4: Generate synthesized response using all gathered information
    var synthesizedResponse = await GenerateSynthesizedResponseAsync(
        userMessage, internalDialog, chatHistory);

    // Step 5: Create the chat response object
    return new ChatResponse
    {
      Id = Guid.NewGuid().ToString(),
      Role = "assistant",
      Content = synthesizedResponse,
      Timestamp = DateTime.UtcNow,
      Internal = internalDialog
    };
  }

  private async Task<bool> DetermineIfToolsNeededAsync(string userMessage, ChatHistory chatHistory)
  {
    var analysisHistory = new ChatHistory();
    analysisHistory.AddSystemMessage(@"
Determine if the user's question requires external tool calls to provide accurate information. 
Consider if the question asks for:
- Current/real-time data
- Specific system information  
- Analysis of external data sources
- Calculations or data processing
- File operations or searches

Respond with only 'YES' if tools are needed, 'NO' if the question can be answered with general knowledge.
");

    analysisHistory.AddUserMessage(userMessage);

    var response = await _chatService.GetChatMessageContentAsync(
        analysisHistory,
        new OpenAIPromptExecutionSettings { MaxTokens = 10, Temperature = 0.1 });

    return response.Content.Trim().ToUpper().StartsWith("YES");
  }

  private List<ExpertPerspective> SelectRelevantPerspectives(string userMessage, bool needsTools)
  {
    var relevantPerspectives = _perspectives
        .Where(p => p.Keywords.Any(keyword =>
            userMessage.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
        .ToList();

    // If tools are needed, ensure we include data_analyst perspective
    if (needsTools && !relevantPerspectives.Any(p => p.Name == "data_analyst"))
    {
      relevantPerspectives.Add(_perspectives.First(p => p.Name == "data_analyst"));
    }

    // Always include at least 2 perspectives, max 4
    if (relevantPerspectives.Count < 2)
    {
      relevantPerspectives.AddRange(_perspectives.Take(2).Except(relevantPerspectives));
    }
    else if (relevantPerspectives.Count > 4)
    {
      relevantPerspectives = relevantPerspectives.Take(4).ToList();
    }

    return relevantPerspectives;
  }

  private async Task<PerspectiveResult> GeneratePerspectiveThoughtsWithToolsAsync(
      ExpertPerspective perspective, string userMessage, ChatHistory chatHistory, bool canUseTools)
  {
    var perspectiveHistory = new ChatHistory();

    var systemPrompt = $@"
{perspective.SystemPrompt}

IMPORTANT: You are providing internal analysis that will be synthesized with other expert perspectives. 
Focus on your domain expertise. Be concise but thorough in your analysis.

{(canUseTools ? "You have access to tools and should use them when they would provide valuable information for your analysis. Use available functions to gather data relevant to your expertise area." : "")}

User Question: {userMessage}

Provide your expert analysis from your specific domain perspective.
";

    perspectiveHistory.AddSystemMessage(systemPrompt);

    // Add relevant chat context
    var recentMessages = chatHistory.TakeLast(4).Where(m => m.Role != AuthorRole.System);
    foreach (var msg in recentMessages)
    {
      perspectiveHistory.Add(msg);
    }

    perspectiveHistory.AddUserMessage(userMessage);

    // Use tool calling settings if tools are available for this perspective
    var settings = canUseTools ? _toolCallingSettings :
        new OpenAIPromptExecutionSettings { MaxTokens = 200, Temperature = 0.7 };

    var response = await _chatService.GetChatMessageContentAsync(perspectiveHistory, settings, _kernel);

    return new PerspectiveResult
    {
      Perspective = perspective.Name,
      Thoughts = response.Content,
      ToolsUsed = ExtractToolCallsFromResponse(response),
      HasToolResults = !string.IsNullOrEmpty(ExtractToolCallsFromResponse(response))
    };
  }

  private string ExtractToolCallsFromResponse(ChatMessageContent response)
  {
    // Extract information about any tool calls that were made
    // This is a simplified version - SK provides more detailed tool call info
    if (response.Metadata?.ContainsKey("tool_calls") == true)
    {
      return response.Metadata["tool_calls"].ToString();
    }
    return string.Empty;
  }

  private InternalDialog CreateInternalDialogWithToolResults(PerspectiveResult[] perspectiveResults)
  {
    var toolResults = perspectiveResults
        .Where(p => p.HasToolResults)
        .Select(p => $"[{p.Perspective}] Used tools: {p.ToolsUsed}")
        .ToList();

    return new InternalDialog
    {
      ReasoningProcess = perspectiveResults.Select(p => new PerspectiveThought
      {
        Perspective = p.Perspective,
        Thoughts = p.Thoughts
      }).ToList(),
      ToolsUsed = toolResults,
      Synthesis = string.Empty,
      FollowUpNeeded = string.Empty
    };
  }

  private async Task<PerspectiveThought> GeneratePerspectiveThoughtsAsync(
      ExpertPerspective perspective, string userMessage, ChatHistory chatHistory)
  {
    var perspectiveHistory = new ChatHistory();
    perspectiveHistory.AddSystemMessage($@"
{perspective.SystemPrompt}

IMPORTANT: You are providing internal analysis that will be synthesized with other expert perspectives. 
Focus on your domain expertise. Be concise but thorough. Limit response to 2-3 sentences.
Consider the user's question from your specific expert viewpoint only.

User Question: {userMessage}
");

    // Add relevant chat context (last 2-3 messages for context)
    var recentMessages = chatHistory.TakeLast(6).Where(m => m.Role != AuthorRole.System);
    foreach (var msg in recentMessages)
    {
      perspectiveHistory.Add(msg);
    }

    var response = await _chatService.GetChatMessageContentAsync(
        perspectiveHistory,
        new OpenAIPromptExecutionSettings
        {
          MaxTokens = 150,
          Temperature = 0.7
        });

    return new PerspectiveThought
    {
      Perspective = perspective.Name,
      Thoughts = response.Content
    };
  }

  private InternalDialog CreateInternalDialog(PerspectiveThought[] perspectiveThoughts)
  {
    return new InternalDialog
    {
      ReasoningProcess = perspectiveThoughts.ToList(),
      Synthesis = string.Empty, // Will be filled during synthesis
      FollowUpNeeded = string.Empty // Will be determined during synthesis
    };
  }

  private async Task<string> GenerateSynthesizedResponseAsync(
      string userMessage, InternalDialog internalDialog, ChatHistory chatHistory)
  {
    var synthesisHistory = new ChatHistory();

    var toolResultsContext = internalDialog.ToolsUsed.Any()
        ? $"\n\nTOOLS USED:\n{string.Join("\n", internalDialog.ToolsUsed)}"
        : "";

    // Main system prompt for synthesis
    synthesisHistory.AddSystemMessage($@"
You are an AI assistant capable of multi-perspective analysis and synthesis. You have consulted with multiple expert perspectives on the user's question, and some experts may have used tools to gather additional information.

EXPERT PERSPECTIVES CONSULTED:
{string.Join("\n", internalDialog.ReasoningProcess.Select(p => $"[{p.Perspective}]: {p.Thoughts}"))}
{toolResultsContext}

RESPONSE GUIDELINES:
- Synthesize the expert insights into a single, natural response
- Incorporate any tool results naturally into your response
- Do NOT mention that you consulted multiple perspectives or used internal tools
- Present the information as if it comes from one knowledgeable source
- Include practical next steps and considerations
- Address potential trade-offs and alternatives
- Use natural transitions that show depth of consideration
- Be authoritative but acknowledge complexity where appropriate

USER QUESTION: {userMessage}

Provide a comprehensive response that seamlessly integrates all the expert perspectives and tool results above.
");

    // Add chat context
    var recentMessages = chatHistory.TakeLast(4).Where(m => m.Role != AuthorRole.System);
    foreach (var msg in recentMessages)
    {
      synthesisHistory.Add(msg);
    }

    // Allow tool use in synthesis if needed for additional context
    var response = await _chatService.GetChatMessageContentAsync(
        synthesisHistory, _toolCallingSettings, _kernel);

    // Update internal dialog with synthesis notes
    var toolsUsedInSynthesis = ExtractToolCallsFromResponse(response);
    internalDialog.Synthesis = "Combined expert perspectives" +
        (internalDialog.ToolsUsed.Any() ? " with tool-gathered data" : "") +
        (toolsUsedInSynthesis != string.Empty ? " and additional synthesis-time tool calls" : "") +
        " to provide comprehensive guidance.";

    internalDialog.FollowUpNeeded = DetermineFollowUpNeeds(response.Content);

    return response.Content;
  }

  private string DetermineFollowUpNeeds(string response)
  {
    // Simple heuristic - look for questions or requests for more info
    if (response.Contains("What") && response.Contains("?"))
      return "Asked clarifying questions to better tailor recommendations.";

    return string.Empty;
  }

  // Method to get streaming responses with internal reasoning
  public async IAsyncEnumerable<string> ProcessUserMessageStreamingAsync(
      string userMessage, ChatHistory chatHistory)
  {
    // Generate internal reasoning first (non-streaming)
    var relevantPerspectives = SelectRelevantPerspectives(userMessage, true);
    var internalReasoningTasks = relevantPerspectives.Select(perspective =>
        GeneratePerspectiveThoughtsAsync(perspective, userMessage, chatHistory));
    var perspectiveThoughts = await Task.WhenAll(internalReasoningTasks);
    var internalDialog = CreateInternalDialog(perspectiveThoughts);

    // Stream the synthesized response
    var synthesisHistory = new ChatHistory();
    synthesisHistory.AddSystemMessage($@"
You are an AI assistant capable of multi-perspective analysis and synthesis. You have consulted with multiple expert perspectives on the user's question. Your task is to create a unified, coherent response that naturally integrates these insights.

EXPERT PERSPECTIVES CONSULTED:
{string.Join("\n", internalDialog.ReasoningProcess.Select(p => $"[{p.Perspective}]: {p.Thoughts}"))}

RESPONSE GUIDELINES:
- Synthesize the expert insights into a single, natural response
- Do NOT mention that you consulted multiple perspectives 
- Present the information as if it comes from one knowledgeable source
- Include practical next steps and considerations

USER QUESTION: {userMessage}
");

    await foreach (var chunk in _chatService.GetStreamingChatMessageContentsAsync(
        synthesisHistory,
        new OpenAIPromptExecutionSettings { MaxTokens = 800, Temperature = 0.8 }))
    {
      if (!string.IsNullOrEmpty(chunk.Content))
      {
        yield return chunk.Content;
      }
    }
  }
}

// Supporting classes
public class ExpertPerspective
{
  public string Name { get; set; }
  public string SystemPrompt { get; set; }
  public string[] Keywords { get; set; }
  public Func<string, bool> CanUseTool { get; set; } = _ => false;
}

public class PerspectiveThought
{
  public string Perspective { get; set; }
  public string Thoughts { get; set; }
}

public class PerspectiveResult
{
  public string Perspective { get; set; }
  public string Thoughts { get; set; }
  public string ToolsUsed { get; set; }
  public bool HasToolResults { get; set; }
}

public class InternalDialog
{
  public List<PerspectiveThought> ReasoningProcess { get; set; }
  public List<string> ToolsUsed { get; set; } = new();
  public string Synthesis { get; set; }
  public string FollowUpNeeded { get; set; }
}

public class ChatResponse
{
  public string Id { get; set; }
  public string Role { get; set; }
  public string Content { get; set; }
  public DateTime Timestamp { get; set; }
  public InternalDialog Internal { get; set; }
}

// Example plugins for demonstration
public class SystemInfoPlugin
{
  [KernelFunction, Description("Get current system performance metrics")]
  public async Task<string> GetSystemMetrics()
  {
    // Simulate getting system metrics
    await Task.Delay(100);
    return "CPU: 45%, Memory: 62%, Disk I/O: 23%";
  }

  [KernelFunction, Description("Check API rate limit status for a service")]
  public async Task<string> CheckRateLimit([Description("The API service name")] string serviceName)
  {
    await Task.Delay(100);
    return $"{serviceName} API: 450/1000 requests used this hour, resets in 23 minutes";
  }
}

public class BusinessDataPlugin
{
  [KernelFunction, Description("Get business metrics and KPIs")]
  public async Task<string> GetBusinessMetrics([Description("Metric type to retrieve")] string metricType)
  {
    await Task.Delay(100);
    return metricType switch
    {
      "revenue" => "Monthly revenue: $125K, 12% increase from last month",
      "users" => "Active users: 15,432 (+8% WoW), Churn rate: 2.1%",
      "performance" => "API response time: 120ms avg, 99.97% uptime",
      _ => "Metric not found"
    };
  }
}

public class WebSearchPlugin
{
  [KernelFunction, Description("Search the web for current information")]
  public async Task<string> SearchWeb([Description("Search query")] string query)
  {
    await Task.Delay(200);
    return $"Search results for '{query}': Latest industry reports show best practices include exponential backoff, circuit breakers, and request queuing for API rate limiting scenarios.";
  }
}

public class TimePlugin
{
  [KernelFunction, Description("Get the current date and time")]
  public string GetCurrentDateTime()
  {
    return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
  }

  [KernelFunction, Description("Get the current UTC date and time")]
  public string GetCurrentUtcDateTime()
  {
    return DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
  }

  [KernelFunction, Description("Get the current date")]
  public string GetCurrentDate()
  {
    return DateTime.Now.ToString("yyyy-MM-dd");
  }

  [KernelFunction, Description("Get the current time")]
  public string GetCurrentTime()
  {
    return DateTime.Now.ToString("HH:mm:ss");
  }

  [KernelFunction, Description("Add days to a date")]
  public string AddDaysToDate([Description("Date in yyyy-MM-dd format")] string date, [Description("Number of days to add")] int days)
  {
    if (DateTime.TryParse(date, out var parsedDate))
    {
      return parsedDate.AddDays(days).ToString("yyyy-MM-dd");
    }
    return "Invalid date format. Please use yyyy-MM-dd format.";
  }

  [KernelFunction, Description("Calculate days between two dates")]
  public string DaysBetweenDates([Description("Start date in yyyy-MM-dd format")] string startDate, [Description("End date in yyyy-MM-dd format")] string endDate)
  {
    if (DateTime.TryParse(startDate, out var start) && DateTime.TryParse(endDate, out var end))
    {
      var daysDifference = (end - start).Days;
      return $"{Math.Abs(daysDifference)} days";
    }
    return "Invalid date format. Please use yyyy-MM-dd format for both dates.";
  }

  [KernelFunction, Description("Get the day of the week for a given date")]
  public string GetDayOfWeek([Description("Date in yyyy-MM-dd format")] string date)
  {
    if (DateTime.TryParse(date, out var parsedDate))
    {
      return parsedDate.DayOfWeek.ToString();
    }
    return "Invalid date format. Please use yyyy-MM-dd format.";
  }

  [KernelFunction, Description("Format a date in a specific format")]
  public string FormatDate([Description("Date in yyyy-MM-dd format")] string date, [Description("Target format (e.g., 'MM/dd/yyyy', 'dd-MM-yyyy')")] string format)
  {
    if (DateTime.TryParse(date, out var parsedDate))
    {
      try
      {
        return parsedDate.ToString(format);
      }
      catch (FormatException)
      {
        return "Invalid format string.";
      }
    }
    return "Invalid date format. Please use yyyy-MM-dd format.";
  }
}

public class MathPlugin
{
  [KernelFunction, Description("Add two numbers")]
  public double Add([Description("First number")] double a, [Description("Second number")] double b)
  {
    return a + b;
  }

  [KernelFunction, Description("Subtract two numbers")]
  public double Subtract([Description("First number")] double a, [Description("Second number")] double b)
  {
    return a - b;
  }

  [KernelFunction, Description("Multiply two numbers")]
  public double Multiply([Description("First number")] double a, [Description("Second number")] double b)
  {
    return a * b;
  }

  [KernelFunction, Description("Divide two numbers")]
  public string Divide([Description("Dividend")] double a, [Description("Divisor")] double b)
  {
    if (b == 0)
    {
      return "Error: Division by zero is not allowed.";
    }
    return (a / b).ToString();
  }

  [KernelFunction, Description("Calculate the power of a number")]
  public double Power([Description("Base number")] double baseNumber, [Description("Exponent")] double exponent)
  {
    return Math.Pow(baseNumber, exponent);
  }

  [KernelFunction, Description("Calculate the square root of a number")]
  public string SquareRoot([Description("Number to calculate square root for")] double number)
  {
    if (number < 0)
    {
      return "Error: Cannot calculate square root of negative number.";
    }
    return Math.Sqrt(number).ToString();
  }

  [KernelFunction, Description("Calculate the absolute value of a number")]
  public double Absolute([Description("Number to get absolute value for")] double number)
  {
    return Math.Abs(number);
  }

  [KernelFunction, Description("Round a number to specified decimal places")]
  public double Round([Description("Number to round")] double number, [Description("Number of decimal places")] int decimals = 2)
  {
    return Math.Round(number, decimals);
  }

  [KernelFunction, Description("Calculate the maximum of two numbers")]
  public double Max([Description("First number")] double a, [Description("Second number")] double b)
  {
    return Math.Max(a, b);
  }

  [KernelFunction, Description("Calculate the minimum of two numbers")]
  public double Min([Description("First number")] double a, [Description("Second number")] double b)
  {
    return Math.Min(a, b);
  }

  [KernelFunction, Description("Calculate percentage")]
  public double Percentage([Description("Part value")] double part, [Description("Total value")] double total)
  {
    if (total == 0)
    {
      return 0;
    }
    return (part / total) * 100;
  }

  [KernelFunction, Description("Calculate percentage change between two values")]
  public string PercentageChange([Description("Original value")] double originalValue, [Description("New value")] double newValue)
  {
    if (originalValue == 0)
    {
      return "Error: Cannot calculate percentage change when original value is zero.";
    }
    var change = ((newValue - originalValue) / originalValue) * 100;
    return $"{Math.Round(change, 2)}%";
  }

  [KernelFunction, Description("Convert degrees to radians")]
  public double DegreesToRadians([Description("Angle in degrees")] double degrees)
  {
    return degrees * (Math.PI / 180);
  }

  [KernelFunction, Description("Convert radians to degrees")]
  public double RadiansToDegrees([Description("Angle in radians")] double radians)
  {
    return radians * (180 / Math.PI);
  }

  [KernelFunction, Description("Calculate factorial of a number")]
  public string Factorial([Description("Non-negative integer")] int number)
  {
    if (number < 0)
    {
      return "Error: Factorial is not defined for negative numbers.";
    }
    if (number > 20)
    {
      return "Error: Number too large for factorial calculation (max 20).";
    }

    long result = 1;
    for (int i = 2; i <= number; i++)
    {
      result *= i;
    }
    return result.ToString();
  }
}

// Usage example
public class Program
{
  public static async Task Main(string[] args)
  {
    IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

    var apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not found in configuration");
    var modelId = "gpt-4.1";

    // Initialize Semantic Kernel with plugins
    var builder = Kernel.CreateBuilder();
    builder.AddOpenAIChatCompletion(modelId, apiKey);

    var kernel = builder.Build();

    // Add plugins
    kernel.ImportPluginFromType<SystemInfoPlugin>("SystemInfo");
    kernel.ImportPluginFromType<BusinessDataPlugin>("BusinessData");
    kernel.ImportPluginFromType<WebSearchPlugin>("WebSearch");
    kernel.ImportPluginFromType<TimePlugin>("Time");
    kernel.ImportPluginFromType<MathPlugin>("Math");

    // Create multi-perspective agent
    var agent = new MultiPerspectiveAgent(kernel);

    // Create chat history
    var chatHistory = new ChatHistory();

    // Process user message that will trigger tool usage
    var userMessage = "We're getting rate limited by our third-party API provider. What's our current system performance and what are the latest best practices?";

    Console.WriteLine($"User: {userMessage}\n");

    var response = await agent.ProcessUserMessageAsync(userMessage, chatHistory);

    Console.WriteLine("Assistant Response:");
    Console.WriteLine(response.Content);

    Console.WriteLine("\n" + "=".PadRight(50, '='));
    Console.WriteLine("Internal Reasoning & Tool Usage:");
    Console.WriteLine("=".PadRight(50, '='));

    foreach (var thought in response.Internal.ReasoningProcess)
    {
      Console.WriteLine($"\n[{thought.Perspective.ToUpper()}]:");
      Console.WriteLine(thought.Thoughts);
    }

    if (response.Internal.ToolsUsed.Any())
    {
      Console.WriteLine("\nTOOLS USED:");
      foreach (var tool in response.Internal.ToolsUsed)
      {
        Console.WriteLine($"- {tool}");
      }
    }

    Console.WriteLine($"\nSYNTHESIS: {response.Internal.Synthesis}");

    // Add to chat history for next turn
    chatHistory.AddUserMessage(userMessage);
    chatHistory.AddAssistantMessage(response.Content);

    // Follow-up question to show context retention
    Console.WriteLine("\n" + "=".PadRight(50, '='));
    Console.WriteLine("Follow-up Question:");
    Console.WriteLine("=".PadRight(50, '='));

    var followUpMessage = "What would be the business impact of implementing these solutions?";
    Console.WriteLine($"\nUser: {followUpMessage}");

    var followUpResponse = await agent.ProcessUserMessageAsync(followUpMessage, chatHistory);
    Console.WriteLine($"\nAssistant: {followUpResponse.Content}");

    if (followUpResponse.Internal.ToolsUsed.Any())
    {
      Console.WriteLine("\nAdditional tools used:");
      followUpResponse.Internal.ToolsUsed.ForEach(Console.WriteLine);
    }
  }
}