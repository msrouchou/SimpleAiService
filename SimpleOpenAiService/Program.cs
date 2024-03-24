using SimpleOpenAiService;
using SimpleOpenAiService.Clients;
using SimpleOpenAiService.Configuration;
using SimpleOpenAiService.Hubs;

var builder = WebApplication.CreateBuilder(args);

var configuration = builder.Configuration;

var ollamaConfig = configuration.GetSection("ollama");

builder.Services.Configure<OllamaConfiguration>(ollamaConfig);

builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(15);
});

builder.Services.AddLogging();

builder.Services.AddHostedService<Worker>();
builder.Services.AddSingleton<OllamaClient>();
builder.Services.AddSingleton<ChatHubService>();
builder.Services.AddSingleton<AiChatHub>();

var app = builder.Build();

app.MapHub<AiChatHub>("/chatHub");

app.Run();
