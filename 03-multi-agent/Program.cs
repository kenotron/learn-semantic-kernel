#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates.

#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates.

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;

using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.Agents.Runtime.InProcess;
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

// 2. Create all agents
var webResearchAgent = await WebResearchAgent.CreateAgent(kernel);
var contentWritingAgent = ContentWritingAgent.CreateAgent(kernel);
var supervisorAgent = SupervisorAgent.CreateAgent(kernel);

Console.WriteLine("Type your content creation requests. Type 'exit' to quit.\n");

var runtime = new InProcessRuntime();
await runtime.StartAsync();

// 3. Create GroupChatOrchestration for coordinated multi-agent collaboration
var groupChatManager = new WorkflowGroupChatManager();

var orchestration = new GroupChatOrchestration(groupChatManager, supervisorAgent, webResearchAgent, contentWritingAgent)
{
    ResponseCallback = (message) =>
    {
        var agentName = message.AuthorName ?? "Agent";
        var emoji = GetAgentEmoji(agentName);
        Console.WriteLine($"{emoji} **{agentName}:**");
        Console.WriteLine($"{message.Content}");
        Console.WriteLine();
        return ValueTask.CompletedTask;
    }
};

// 3. Start the orchestration
var userInput = "I need a comprehensive article about the latest advancements in AI and machine learning, including recent research findings and practical applications.";
var result = await orchestration.InvokeAsync(userInput, runtime);
string text = await result.GetValueAsync();

await runtime.RunUntilIdleAsync();
