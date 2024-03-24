using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using SimpleOpenAiService.Clients;
using System;
using System.Threading.Tasks;

namespace SimpleOpenAiService.Hubs;

public class ChatHubService
{
    private readonly HubConnection _hubConnection;
    private readonly ILogger _logger;

    public ChatHubService(
        ILogger<ChatHubService> logger,
        OllamaClient ollamaClient)
    {
        _logger = logger;
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

    public async Task EnsureConnectionAsync(CancellationToken cancellationToken)
    {
        LogInitialConnectionState();

        while (_hubConnection.State != HubConnectionState.Connected && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                await _hubConnection.StartAsync();
                _logger.LogInformation($"(｡◕‿‿◕｡) Connected to {_hubConnection.ConnectionId}!");
            }
            catch (Exception)
            {
                _logger.LogWarning($"*** Not Connected: {_hubConnection.State}");
                await Task.Delay(TimeSpan.FromSeconds(5));
            }
        }

        void LogInitialConnectionState()
        {
            if (_hubConnection.State == HubConnectionState.Connected)
                _logger.LogInformation($"*** EnsureConnectionAsync: {_hubConnection.State}");
            else
                _logger.LogWarning($"*** EnsureConnectionAsync: {_hubConnection.State}");
        }
    }
}
