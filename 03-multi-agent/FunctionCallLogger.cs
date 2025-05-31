
using Microsoft.SemanticKernel;

public class FunctionCallLogger : IFunctionInvocationFilter
{
    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        Console.WriteLine($"🔧 Calling function: {context.Function.PluginName}.{context.Function.Name}");
        if (context.Arguments.Count > 0)
        {
            Console.WriteLine($"   Arguments: {string.Join(", ", context.Arguments.Select(kvp => $"{kvp.Key}: {kvp.Value}"))}");
        }
        
        await next(context);
        
        Console.WriteLine($"✅ Function successful");
        Console.WriteLine();
    }
}