using System.Diagnostics;
using System.Runtime.CompilerServices;
using Meziantou.Framework;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using OllamaSharp.Streamer;
using SimpleOpenAiService.Configuration;
using SimpleOpenAiService.Hubs;
using static SimpleOpenAiService.Hubs.AiChatHub;

namespace SimpleOpenAiService.Clients;

public sealed class OllamaClient
{
    public OllamaClient(
        ILogger<OllamaClient> logger,
        IOptions<OllamaConfiguration> ollamaConfig,
        AiChatHub aiChatHub)
    {
        _logger = logger;
        _ollamaConfig = ollamaConfig;
        _aiChatHub = aiChatHub;
        _ollamaApi = new(ollamaConfig.Value.Uri, ollamaConfig.Value.Model);

        _aiChatHub.UserMessageReceived += OnUserMessageReceived;
    }

    public CancellationTokenSource CancellationSource = new();

    private async void OnUserMessageReceived(object? sender, UserMessageReceivedEventArgs e)
    {
        try
        {
            _ = await StreamCompletion(e.Prompt, CancellationSource.Token);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // todo: polly, model not ready yet
        }
    }

    private readonly OllamaApiClient _ollamaApi;
    private readonly ILogger<OllamaClient> _logger;
    private readonly IOptions<OllamaConfiguration> _ollamaConfig;
    private readonly AiChatHub _aiChatHub;

    public async Task EnsureModelExists(bool mustPullModel, CancellationToken cancellationToken)
    {
        IEnumerable<Model> localModels = await EnsureOllamaIsRunning(cancellationToken);

        string modelName = _ollamaConfig.Value.Model;

        if (localModels.Any(m =>
        {
            return m.Name.StartsWith(modelName);
        }))
        {
            _logger.LogInformation("Local Ollama model ready: {ModelName} ", modelName);
            return;
        }

        if (!mustPullModel)
        {
            _logger.LogInformation("Bailing out from pulling the model as instruced");
            return;
        }

        await PullModel(cancellationToken);

        // Local Function(s)
        async Task PullModel(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Pulling model '{modelName}'...");
            var sw = Stopwatch.StartNew();

            await _ollamaApi.PullModel(
                modelName,
                status => _logger.LogInformation($"({status.Percent}%) {status.Status}"),
                cancellationToken);

            sw.Stop();
            var elasped = sw.Elapsed;

            var localModels = await _ollamaApi.ListLocalModels(cancellationToken);

            if (localModels.Any(m => m.Name.EqualsIgnoreCase(modelName)))
            {
                _logger.LogDebug($"Ollama model '{modelName}' was successfully pulled locally in '{elasped.TotalMinutes}' minutes");
                return;
            }
        }

        async Task<IEnumerable<Model>> EnsureOllamaIsRunning(CancellationToken cancellationToken)
        {
            IEnumerable<Model> localModels = [];
            bool isOllamaRunning = false;
            do
            {
                try
                {
                    localModels = await _ollamaApi.ListLocalModels(cancellationToken);
                    isOllamaRunning = true;
                }
                catch (HttpRequestException e)
                {
                    _logger.LogError($"Ollama is not running: {e.Message}");
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                }
            } while (!isOllamaRunning);

            return localModels;
        }
    }

    public async Task<IEnumerable<string>> StreamCompletion(string prompt, CancellationToken cancellationToken)
    {
        List<string> answer = [];

        ConversationContext? context = null;
        context = await _ollamaApi.StreamCompletion(
            prompt,
            context,
            async stream =>
            {
                answer.Add(stream.Response);

                await _aiChatHub.SendBotMessage("bot", stream.Response);

                if (stream.Done)
                {
                    await _aiChatHub.SendBotMessage("bot", "%%%DONE%%");
                }
            },
            cancellationToken);

        _logger.LogInformation(string.Join("", answer));

        return answer;
    }

    //public async IAsyncEnumerable<string> StreamCompletion2(string prompt, [EnumeratorCancellation] CancellationToken cancellationToken)
    //{
    //    ConversationContext? context = null;
    //    GenerateCompletionRequest request = new() { Context = context?.Context, Prompt = prompt };
    //    IResponseStreamer<GenerateCompletionResponseStream> streamer = new ;
    //    await foreach (var stream in _ollamaApi.StreamCompletion(request, streamer, cancellationToken))
    //    {
    //        logger.LogInformation(stream.Response);
    //        yield return stream.Response;
    //    }
    //}

}
