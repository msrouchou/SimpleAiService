﻿using SimpleOpenAiService.Hubs;
using static SimpleOpenAiService.Hubs.AiChatHub;

namespace SimpleOpenAiService.Clients;

public abstract class AiClientBase
{
    private readonly AiChatHub _aiChatHub;

    protected AiClientBase(AiChatHub aiChatHub)
    {
        _aiChatHub = aiChatHub;
        OnInitialized();
    }

    public CancellationTokenSource ChatCancellationSource = new();

    public abstract Task StreamCompletion(string user, string prompt, CancellationToken cancellationToken);

    public abstract Task StreamChat(string user, string prompt, CancellationToken cancellationToken);

    public abstract Task EnsureModelExists(bool mustPullModel, CancellationToken cancellationToken);

    private void OnInitialized() => _aiChatHub.UserMessageReceived += OnUserMessageReceived;

    private async void OnUserMessageReceived(object? sender, UserMessageReceivedEventArgs e)
    {
        try
        {
            await StreamChat(e.User, e.Prompt, ChatCancellationSource.Token);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // todo: polly, model not ready yet
        }
    }
}