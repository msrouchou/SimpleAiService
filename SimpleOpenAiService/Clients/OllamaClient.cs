using System.Collections.Concurrent;
using System.Diagnostics;
using Meziantou.Framework;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models;
using SimpleOpenAiService.Configuration;
using SimpleOpenAiService.Hubs;

namespace SimpleOpenAiService.Clients;

public sealed class OllamaClient(
        ILogger<OllamaClient> logger,
        IOptions<OllamaConfiguration> ollamaConfig,
        AiChatHub aiChatHub)
    : AiClientBase(aiChatHub)
{
    private readonly OllamaApiClient _ollamaApi = new(ollamaConfig.Value.Uri, ollamaConfig.Value.Model);
    private readonly ILogger<OllamaClient> _logger = logger;
    private readonly AiChatHub _aiChatHub = aiChatHub;
    private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _unansweredPromptsByUser = new();

    public override async Task StreamCompletion(string user, string prompt, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            EnqueueUserPrompt(user, prompt);
            return;
        }

        await StreamCompletionForQueuedPrompts(cancellationToken);

        List<string> answerDebug = [];

        ConversationContext? context = null;
        context = await Task.Run(async () =>
        {
            return await _ollamaApi.StreamCompletion(
                prompt,
                context,
                async stream =>
                {
                    await _aiChatHub.SendBotAnswer(user, stream.Response, isDone: stream.Done);

                    _logger.LogDebug($"{{Response}}{(stream.Done ? "<{{IsDone}}>" : "")}", stream.Response, stream.Done);
                    answerDebug.Add(stream.Response);
                },
                cancellationToken);
        });

        _logger.LogInformation(string.Join("", answerDebug));

        #region LocalFunctions

        void EnqueueUserPrompt(string user, string prompt)
        {
            if (_unansweredPromptsByUser.TryGetValue(user, out var unansweredPrompts))
            {
                if (unansweredPrompts.Count < 3)
                {
                    _unansweredPromptsByUser[user].Enqueue(prompt);
                }
            }
            else
            {
                _unansweredPromptsByUser[user] = new ConcurrentQueue<string>([prompt]);
            }
        }

        async Task StreamCompletionForQueuedPrompts(CancellationToken cancellationToken)
        {
            while (!_unansweredPromptsByUser.IsEmpty)
            {
                foreach (var queuedUser in _unansweredPromptsByUser.Keys)
                {
                    if (_unansweredPromptsByUser[queuedUser].TryDequeue(out var unanswered))
                    {
                        await StreamCompletion(queuedUser, unanswered, cancellationToken);
                    }
                }
            }
        }

        #endregion
    }

    public override async Task EnsureModelExists(bool mustPullModel, CancellationToken cancellationToken)
    {
        IEnumerable<Model> localModels = await EnsureOllamaIsRunning(cancellationToken);

        var modelName = ollamaConfig.Value.Model;

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
