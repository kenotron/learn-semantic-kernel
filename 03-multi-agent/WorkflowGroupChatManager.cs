#pragma warning disable SKEXP0001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.


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
                Console.Write("User Feedback > ");
                var userInput = Console.ReadLine();
                return ValueTask.FromResult(new ChatMessageContent(role: AuthorRole.User, userInput ?? string.Empty));
            };
    }

    public override ValueTask<GroupChatManagerResult<bool>> ShouldRequestUserInput(ChatHistory history, CancellationToken cancellationToken = default)
    {

        string? lastAgent = history.LastOrDefault()?.AuthorName;

        if (lastAgent is null)
        {
            return ValueTask.FromResult(new GroupChatManagerResult<bool>(false) { Reason = "No agents have spoken yet." });
        }

        if (lastAgent == "ProjectSupervisor")
        {
            return ValueTask.FromResult(new GroupChatManagerResult<bool>(true) { Reason = "User input is needed after the reviewer's message." });
        }

        return ValueTask.FromResult(new GroupChatManagerResult<bool>(false) { Reason = "User input is not needed until the reviewer's message." });
    }
}
