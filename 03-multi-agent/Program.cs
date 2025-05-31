using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Agents;
using WebSearchPluginExtensions;
using MultiAgent.Agents.WebResearchAgent;

// Load configuration from appsettings.json and user secrets
IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

var apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not found in configuration");
var modelId = "gpt-4.1";

// 1. Create the kernel with HttpClient and WebSearchPlugin
var builder = Kernel.CreateBuilder().AddOpenAIChatCompletion(modelId, apiKey);
builder.Services.AddSingleton<IFunctionInvocationFilter, FunctionCallLogger>();
builder.Services.AddHttpClient();

Kernel kernel = builder.Build();

// 2. Add the WebSearchPlugin to the kernel with automatic fallback
await kernel.AddWebSearchPluginAsync(
    localSearxngUrl: "http://10.0.0.251:30053/",
    publicSearxngUrl: "https://searx.be");

// 3. Add the WebPageAnalysisPlugin to the kernel
var httpClientForAnalysis = kernel.Services.GetRequiredService<HttpClient>();
kernel.Plugins.AddFromObject(new WebPageAnalysisPlugin(httpClientForAnalysis), "WebPageAnalysis");

// 4. Display plugin information
kernel.DisplayWebSearchPluginInfo();
Console.WriteLine($"✅ Added WebPageAnalysisPlugin with AnalyzePageAsync function");
Console.WriteLine($"📊 Total plugins loaded: {kernel.Plugins.Count}");

// 5. Create the Web Deep Research Expert agent
var webResearchAgent = WebResearchAgent.CreateAgent(kernel);

Console.WriteLine("=== Web Deep Research Expert Agent ===");
Console.WriteLine("Type your research questions or topics. The agent will search for current information and provide comprehensive analysis.");
Console.WriteLine("Type 'exit' to quit.\n");

// 6. Create a ChatHistoryAgentThread for managing the conversation
var agentThread = new ChatHistoryAgentThread();

string? userInput;

do
{
  Console.Write("Research Query > ");
  userInput = Console.ReadLine();

  if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit")
  {
    break;
  }

  Console.WriteLine("\n🔍 Researching...\n");

  // Invoke the agent with the user message directly
  await foreach (var message in webResearchAgent.InvokeAsync(userInput, agentThread))
  {
    Console.WriteLine($"🧠 Research Expert: {message.Message.Content}");
  }

  Console.WriteLine("\n" + new string('-', 80) + "\n");

} while (true);