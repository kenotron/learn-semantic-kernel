
// Custom Group Chat Manager with Human-in-the-Loop functionality
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents.Orchestration;
using Microsoft.SemanticKernel.Agents.Orchestration.GroupChat;
using Microsoft.SemanticKernel.ChatCompletion;


#pragma warning disable SKEXP0110 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
public sealed class WorkflowGroupChatManager : RoundRobinGroupChatManager
{
    public WorkflowGroupChatManager()
    {
        MaximumInvocationCount = 20; // Equivalent to the old max iterations
        InteractiveCallback =
            () =>
            {
                // Custom logic for human-in-the-loop interaction
                Console.Write("User >");
                var userInput = Console.ReadLine();
                return ValueTask.FromResult(new ChatMessageContent(role: AuthorRole.User, userInput ?? string.Empty));
            };
    }

    public override ValueTask<GroupChatManagerResult<bool>> ShouldTerminate(ChatHistory history, CancellationToken cancellationToken = default)
    {
        var lastMessage = history.LastOrDefault();
        bool shouldTerminate = lastMessage?.Content?.Contains("WORKFLOW_COMPLETE", StringComparison.OrdinalIgnoreCase) == true;

        return ValueTask.FromResult(new GroupChatManagerResult<bool>(shouldTerminate)
        {
            Reason = shouldTerminate ? "Workflow completed successfully." : "Workflow still in progress."
        });
    }
}
