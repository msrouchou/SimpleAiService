using SimpleOpenAiService.Clients;
using SimpleOpenAiService.Hubs;

namespace SimpleOpenAiService;

public sealed class Worker(ChatHubService chatService, OllamaClient ollamaClient)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await ollamaClient.EnsureModelExists(mustPullModel: true, cancellationToken);
            await chatService.EnsureConnectionAsync(cancellationToken);

            await Task.Delay(5000, cancellationToken);
        }
    }
}
