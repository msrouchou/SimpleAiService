using Microsoft.AspNetCore.SignalR;

namespace SimpleOpenAiService.Hubs;

public sealed class AiChatHub(
    ILogger<AiChatHub> logger)
    : Hub
{
    private readonly IDictionary<string, string> _userConnections = new Dictionary<string, string>();

    internal event EventHandler<UserMessageReceivedEventArgs>? UserMessageReceived;

    /// <summary>
    /// Recipe:
    ///     - Receive user prompt
    ///     - Feed prompt to AI and get answer
    ///     - Send answer back to Hub clients
    /// </summary>
    /// <param name="user">The user name</param>
    /// <param name="prompt">The user prompt</param>
    public void ReceiveUserPrompt(string user, string prompt)
    {
        logger.LogInformation($"Received prompt from {{User}}: {prompt}", user);

        var connectionId = Context.ConnectionId;

        if (_userConnections.ContainsKey(user))
        {
            _userConnections[user] = connectionId;
        }
        else
        {
            _userConnections.Add(user, connectionId);
        }

        UserMessageReceived?.Invoke(this, new UserMessageReceivedEventArgs(user, prompt));
    }

    internal async Task SendBotAnswer(string user, string answer)
    {
        if (_userConnections.TryGetValue(user, out var userConnectionId))
        {
            await Clients.Client(userConnectionId).SendAsync("ReceiveBotMessage", user, answer);
        }
        else
        {
            logger.LogError("Connection not found for user: {User}", user);
        }
    }

    internal class UserMessageReceivedEventArgs(string user, string prompt) : EventArgs
    {
        public string User { get; } = user;
        public string Prompt { get; } = prompt;
    }
}
