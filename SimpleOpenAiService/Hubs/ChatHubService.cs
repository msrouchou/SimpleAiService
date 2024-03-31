using Microsoft.AspNetCore.SignalR.Client;
using SimpleOpenAiService.Clients;

namespace SimpleOpenAiService.Hubs;

public class ChatHubService
{
    private readonly HubConnection _hubConnection;
    private readonly ILogger _logger;
    private readonly OllamaClient _ollamaClient;

    public ChatHubService(
        ILogger<ChatHubService> logger,
        OllamaClient ollamaClient)
    {
        _logger = logger;
        _ollamaClient = ollamaClient;
        _hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost:5217/chatHub")
            .Build();

        _hubConnection.On<string, string>("ReceiveUserMessage", (user, message) =>
        {
            // todo: delete
            _logger.LogWarning($"_hubConnection.On<string, string>(ReceiveUserMessage,... {user}: {message}");
        });

        _hubConnection.On<string, string>("SendBotMessage", (user, message) =>
        {
            _logger.LogWarning($"_hubConnection.On<string, string>(SendBotMessage,... {user}: {message}");
        });
    }

    public async Task EnsureSignalRConnectionAsync(CancellationToken cancellationToken)
    {
        LogInitialConnectionState();

        while (_hubConnection.State != HubConnectionState.Connected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _hubConnection.StartAsync();
                _logger.LogInformation("(｡◕‿‿◕｡) Connected to {ConnectionId}!", _hubConnection.ConnectionId);
            }
            catch (Exception)
            {
                _logger.LogWarning("Not Connected: {State}", _hubConnection.State);
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        void LogInitialConnectionState()
        {
            if (_hubConnection.State == HubConnectionState.Connected)
            {
                if (_ollamaClient.CancellationSource.IsCancellationRequested)
                {
                    _ollamaClient.CancellationSource = new();
                }

                _logger.LogInformation($"{nameof(EnsureSignalRConnectionAsync)}: {{State}}", _hubConnection.State);
            }
            else
            {
                _ollamaClient.CancellationSource.Cancel();
                _logger.LogWarning($"{nameof(EnsureSignalRConnectionAsync)}: {{State}}", _hubConnection.State);
            }
        }
    }
}
