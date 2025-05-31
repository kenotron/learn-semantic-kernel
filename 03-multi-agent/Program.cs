#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning disable SKEXP0101 // Type is for evaluation purposes only and is subject to change or removal in future updates.
#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates.

using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Agents.Chat;
using WebSearchPluginExtensions;
using MultiAgent.Agents.WebResearchAgent;
using MultiAgent.Agents.ContentWritingAgent;
using MultiAgent.Agents.SupervisorAgent;

// Helper method to get emoji for agent
static string GetAgentEmoji(string agentName) => agentName switch
{
    "ProjectSupervisor" => "👔",
    "WebResearchExpert" => "🔍", 
    "ContentWritingExpert" => "✍️",
    "User" => "👤",
    _ => "🤖"
};

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

// 5. Create all agents
var webResearchAgent = WebResearchAgent.CreateAgent(kernel);
var contentWritingAgent = ContentWritingAgent.CreateAgent(kernel);
var supervisorAgent = SupervisorAgent.CreateAgent(kernel);

Console.WriteLine("=== Multi-Agent Content Creation System ===");
Console.WriteLine("Available agents:");
Console.WriteLine("  🔍 Web Research Expert - Conducts comprehensive web research");
Console.WriteLine("  ✍️  Content Writing Expert - Creates high-quality written content");
Console.WriteLine("  👔 Project Supervisor - Coordinates between research and writing teams");
Console.WriteLine();
Console.WriteLine("Using AgentGroupChat for coordinated multi-agent collaboration.");
Console.WriteLine("Type your content creation requests. Type 'exit' to quit.\n");

// 6. Create AgentGroupChat for coordinated multi-agent collaboration
var groupChat = new AgentGroupChat(supervisorAgent, webResearchAgent, contentWritingAgent)
{
    ExecutionSettings = new()
    {
        // Use a sequential selection strategy starting with supervisor
        SelectionStrategy = new SequentialSelectionStrategy()
        {
            // Always start with supervisor for initial planning
            InitialAgent = supervisorAgent
        },
        // Set termination condition
        TerminationStrategy = new RegexTerminationStrategy("WORKFLOW_COMPLETE")
        {
            // Maximum of 10 turns to prevent infinite loops
            MaximumIterations = 20
        }
    }
};

string? userInput;

do
{
    Console.Write("Content Request > ");
    userInput = Console.ReadLine();

    if (string.IsNullOrWhiteSpace(userInput) || userInput.ToLower() == "exit")
    {
        break;
    }    Console.WriteLine("\n🚀 Starting Multi-Agent Collaboration...\n");

    // Add the user request to the group chat
    groupChat.AddChatMessage(new ChatMessageContent(AuthorRole.User, 
        $"Content creation request: {userInput}\n\n" +
        "Instructions for the team:\n" +
        "1. Supervisor: Create a project plan and coordinate the workflow\n" +
        "2. Research Expert: Conduct comprehensive research based on the plan\n" +
        "3. Content Writer: Create high-quality content using the research\n" +
        "4. Supervisor: Review and approve the final deliverable\n" +
        "When the workflow is complete, end with 'WORKFLOW_COMPLETE'"));

    // Execute the group chat workflow with streaming
    string? currentAgent = null;
    var messageBuffer = new StringBuilder();
    
    try 
    {
        await foreach (var streamingMessage in groupChat.InvokeStreamingAsync())
        {
            // Check if this is from a new agent
            var agentName = streamingMessage.AuthorName ?? "Agent";
            
            if (currentAgent != agentName)
            {
                // If we have content from previous agent, display it
                if (currentAgent != null && messageBuffer.Length > 0)
                {
                    Console.WriteLine($"\n{GetAgentEmoji(currentAgent)} **{currentAgent}:** {messageBuffer}");
                    Console.WriteLine();
                }
                
                // Start new agent
                currentAgent = agentName;
                messageBuffer.Clear();
                Console.Write($"\n{GetAgentEmoji(agentName)} **{agentName}:** ");
            }
            
            // Stream the content in real-time
            if (!string.IsNullOrEmpty(streamingMessage.Content))
            {
                Console.Write(streamingMessage.Content);
                messageBuffer.Append(streamingMessage.Content);
            }
        }
        
        // Display any remaining content
        if (currentAgent != null && messageBuffer.Length > 0)
        {
            Console.WriteLine("\n");
        }
    }
    catch (NotSupportedException)
    {
        // Fallback to non-streaming if streaming is not supported
        Console.WriteLine("⚠️  Streaming not supported, falling back to batch processing...\n");
        
        await foreach (var message in groupChat.InvokeAsync())
        {
            var agentName = message.AuthorName ?? "Agent";
            var emoji = GetAgentEmoji(agentName);
            Console.WriteLine($"{emoji} **{agentName}:**");
            Console.WriteLine($"{message.Content}");
            Console.WriteLine();
        }
    }
    
    Console.WriteLine($"\n{new string('-', 80)}\n");

} while (true);