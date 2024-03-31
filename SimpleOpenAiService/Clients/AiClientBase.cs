using SimpleOpenAiService.Hubs;
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

    public CancellationTokenSource CancellationSource = new();

    public abstract Task StreamCompletion(string prompt, CancellationToken cancellationToken);

    public abstract Task EnsureModelExists(bool mustPullModel, CancellationToken cancellationToken);

    private void OnInitialized() => _aiChatHub.UserMessageReceived += OnUserMessageReceived;

    private async void OnUserMessageReceived(object? sender, UserMessageReceivedEventArgs e)
    {
        try
        {
            await StreamCompletion(e.Prompt, CancellationSource.Token);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // todo: polly, model not ready yet
        }
    }
}