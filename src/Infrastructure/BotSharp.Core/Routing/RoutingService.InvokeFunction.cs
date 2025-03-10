using BotSharp.Abstraction.Functions;
namespace BotSharp.Core.Routing;

public partial class RoutingService
{
    public async Task<bool> InvokeFunction(string name, RoleDialogModel message)
    {
        var function = _services.GetServices<IFunctionCallback>().FirstOrDefault(x => x.Name == name);
        if (function == null)
        {
            message.StopCompletion = true;
            message.Content = $"Can't find function implementation of {name}.";
            _logger.LogError(message.Content);
            return false;
        }

        // Clone message
        var clonedMessage = RoleDialogModel.From(message);
        clonedMessage.FunctionName = name;

        var hooks = _services
            .GetRequiredService<ConversationHookProvider>()
            .HooksOrderByPriority;

        var progressService = _services.GetService<IConversationProgressService>();

        // Before executing functions
        clonedMessage.Indication = await function.GetIndication(message);
        if (progressService?.OnFunctionExecuting != null)
        {
            await progressService.OnFunctionExecuting(clonedMessage);
        }
        
        foreach (var hook in hooks)
        {
            await hook.OnFunctionExecuting(clonedMessage);
        }

        bool result = false;

        try
        {
            result = await function.Execute(clonedMessage);

            // After functions have been executed
            foreach (var hook in hooks)
            {
                await hook.OnFunctionExecuted(clonedMessage);
            }

            // Set result to original message
            message.Role = AgentRole.Function;
            message.PostbackFunctionName = clonedMessage.PostbackFunctionName;
            message.CurrentAgentId = clonedMessage.CurrentAgentId;
            message.Content = clonedMessage.Content;
            // Don't copy payload
            // message.Payload = clonedMessage.Payload;
            message.StopCompletion = clonedMessage.StopCompletion;
            message.RichContent = clonedMessage.RichContent;
            message.Data = clonedMessage.Data;
        }
        catch (JsonException ex)
        {
            _logger.LogError($"The input does not contain any JSON tokens:\r\n{message.Content}");
            message.StopCompletion = true;
            message.Content = ex.Message;
        }
        catch (Exception ex)
        {
            message.StopCompletion = true;
            message.Content = ex.Message;
            _logger.LogError(ex.ToString());
        }

        // Make sure content has been populated
        if (string.IsNullOrEmpty(message.Content) && message.Data != null)
        {
            message.Content = JsonSerializer.Serialize(message.Data);
        }

        // Save to Storage as well
        /*if (!message.StopCompletion && message.FunctionName != "route_to_agent")
        {
            var storage = _services.GetRequiredService<IConversationStorage>();
            storage.Append(Context.ConversationId, message);
        }*/

        return result;
    }
}
