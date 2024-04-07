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
    private readonly ConcurrentDictionary<string, Chat> _chatsByUser = new();
    private string? _loadedModel;

    public override async Task StreamCompletion(string user, string prompt, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            //EnqueueUserPrompt(user, prompt);
            _logger.LogWarning("Completion cancellation requeted for {User}", user);
            return;
        }

        //await StreamCompletionForQueuedPrompts(cancellationToken);

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

                    //_logger.LogDebug($"{{Response}}{(stream.Done ? "<IsDone>" : "")}", stream.Response);
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
            var users = _unansweredPromptsByUser.Keys.ToList(); // Convert keys to list for round-robin iteration
            var currentIndex = 0;

            while (!_unansweredPromptsByUser.IsEmpty && _unansweredPromptsByUser.Values.Count > 0)
            {
                var queuedUser = users[currentIndex];

                if (_unansweredPromptsByUser[queuedUser].TryDequeue(out var unanswered))
                {
                    await StreamCompletion(queuedUser, unanswered, cancellationToken);
                }

                currentIndex = (currentIndex + 1) % users.Count; // Move to the next user in a round-robin fashion
            }
        }


        #endregion
    }

    public override async Task StreamChat(string user, string prompt, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Chat cancellation requeted for {User}", user);
            return;
        }

        List<string> answerDebug = [];

        if (!_chatsByUser.TryGetValue(user, out var chat))
        {
            // Start a new user Chat
            chat = _ollamaApi.Chat(async stream =>
            {
                await _aiChatHub.SendBotAnswer(user, stream.Message.Content, stream.Done);

                answerDebug.Add(stream.Message.Content);
            });


            if (!_chatsByUser.TryAdd(user, chat))
            {
                _logger.LogError("Failed to keep track of chat for {User}", user);
            }
        }

        var history = await chat.Send(prompt, cancellationToken);

        _logger.LogInformation(string.Join("", answerDebug));
    }

    public override async Task EnsureModelExists(bool mustPullModel, CancellationToken cancellationToken)
    {
        IEnumerable<Model> localModels = await EnsureOllamaIsRunning(cancellationToken);

        var configuredModelName = ollamaConfig.Value.Model;

        if (_loadedModel.EqualsIgnoreCase(configuredModelName))
            return;

        if (localModels.Any(m =>
        {
            return m.Name.StartsWith(configuredModelName);
        }))
        {
            _logger.LogInformation("Local Ollama model ready: {ModelName} ", configuredModelName);
            _loadedModel = configuredModelName;
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
                    _loadedModel = null;
                    await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                }
            } while (!isOllamaRunning);

            return localModels;
        }

        async Task PullModel(CancellationToken cancellationToken)
        {
            _logger.LogInformation($"Pulling model '{configuredModelName}'...");
            var sw = Stopwatch.StartNew();

            await _ollamaApi.PullModel(
                configuredModelName,
                status => _logger.LogInformation($"({status.Percent}%) {status.Status}"),
                cancellationToken);

            sw.Stop();
            var elasped = sw.Elapsed;

            var localModels = await _ollamaApi.ListLocalModels(cancellationToken);

            if (localModels.Any(m => m.Name.EqualsIgnoreCase(configuredModelName)))
            {
                _logger.LogDebug($"Ollama model '{configuredModelName}' was successfully pulled locally in '{elasped.TotalMinutes}' minutes");
                return;
            }
        }
    }
}
