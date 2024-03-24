using Microsoft.AspNetCore.SignalR;
using SimpleOpenAiService.Clients;

namespace SimpleOpenAiService.Hubs;

public class AiChatHub(
    ILogger<AiChatHub> logger)
    : Hub
{
    public event EventHandler<UserMessageReceivedEventArgs>? UserMessageReceived;

    /// <summary>
    /// Recipe:
    ///     - Receive user prompt
    ///     - Feed prompt to AI and get answer
    ///     - Send answer back to Hub clients
    /// </summary>
    /// <param name="user">The user name</param>
    /// <param name="prompt">The user prompt</param>
    public async Task ReceiveUserPrompt(string user, string prompt)
    {
        logger.LogInformation($"Received prompt from {user}: {prompt}");

        //var answer = await ollamaClient.StreamCompletion(prompt, CancellationToken.None);
        await Task.Yield();
        OnUserMessageReceived(new UserMessageReceivedEventArgs(user, prompt));
    }

    public async Task SendBotMessage(string user, string response)
    {
        await Clients.All.SendAsync("ReceiveBotMessage", user, response);
    }

    protected virtual void OnUserMessageReceived(UserMessageReceivedEventArgs e)
    {
        UserMessageReceived?.Invoke(this, e);
    }

    public class UserMessageReceivedEventArgs(string user, string prompt)
        : EventArgs
    {
        public string User { get; } = user;
        public string Prompt { get; } = prompt;
    }
}
