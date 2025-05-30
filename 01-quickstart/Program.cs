using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;


// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

// Load configuration from appsettings.json and user secrets
IConfiguration configuration = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddUserSecrets<Program>()
    .Build();

var builder = Kernel.CreateBuilder();
builder.Services.AddLogging(configure => configure.AddConsole());
builder.AddOpenAIChatCompletion(
  modelId: "gpt-4.1",
  apiKey: configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API key not found in configuration")
);

Kernel kernel = builder.Build();
var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

var history = new ChatHistory();

OpenAIPromptExecutionSettings openAIPromptExecutionSettings = new()
{
  FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
};

// Initiate a back-and-forth chat
string? userInput;
do
{
  // Collect user input
  Console.Write("User > ");
  userInput = Console.ReadLine()!;

  // Add user input
  history.AddUserMessage(userInput);

  // Get the response from the AI
  var streaming = chatCompletionService.GetStreamingChatMessageContentsAsync(history, executionSettings: openAIPromptExecutionSettings, kernel: kernel);
  var isFirstChunk = true;

  await foreach (var chunk in streaming)
  {
    // If this is the first chunk, print a new line
    if (isFirstChunk)
    {
      Console.Write("Assistant > ");
      isFirstChunk = false;
    }

    // Print the response as it comes in
      Console.Write(chunk.Content);
  }

  Console.WriteLine(); // New line after the response
} while (userInput is not null);