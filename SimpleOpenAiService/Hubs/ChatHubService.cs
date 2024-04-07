using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using SimpleOpenAiService.Clients;
using SimpleOpenAiService.Configuration;

namespace SimpleOpenAiService.Hubs;

public class ChatHubService
{
    private readonly HubConnection _hubConnection;
    private readonly ILogger _logger;
    private readonly OllamaClient _ollamaClient;
    private HubConnectionState? _previousConnectionState = null;

    public ChatHubService(
        ILogger<ChatHubService> logger,
        IOptions<ClientHubConfiguration> clientHubConfig,
        OllamaClient ollamaClient)
    {
        _logger = logger;
        _ollamaClient = ollamaClient;
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(clientHubConfig.Value.Uri)
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

        // todo: use _hubConnection.EVENTS
    }

    public async Task EnsureSignalRConnectionAsync(CancellationToken cancellationToken)
    {
        LogInitialConnectionState();

        while (_hubConnection.State != HubConnectionState.Connected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _hubConnection.StartAsync();
                _logger.LogInformation("⚡💻 Connected to {ConnectionId}!", _hubConnection.ConnectionId);
            }
            catch (Exception)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }

        void LogInitialConnectionState()
        {
            if (_previousConnectionState == _hubConnection.State)
                return;

            _previousConnectionState = _hubConnection.State;

            if (_hubConnection.State == HubConnectionState.Connected)
            {
                if (_ollamaClient.ChatCancellationSource.IsCancellationRequested)
                {
                    _ollamaClient.ChatCancellationSource = new();
                }

                _logger.LogInformation($"UI application: {{State}}", _hubConnection.State);
            }
            else
            {
                _ollamaClient.ChatCancellationSource.Cancel();

                _logger.LogWarning($"UI application: {{State}}", _hubConnection.State);
            }
        }
    }
}
