using Serilog;
using SimpleOpenAiService;
using SimpleOpenAiService.Clients;
using SimpleOpenAiService.Configuration;
using SimpleOpenAiService.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Worker
builder.Services.AddHostedService<Worker>();

// Serilog
builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

// Ollama
builder.Services.Configure<OllamaConfiguration>(builder.Configuration.GetSection("Ollama"));
builder.Services.AddSingleton<OllamaClient>();

// SignalR
builder.Services.Configure<ClientHubConfiguration>(builder.Configuration.GetSection("ClientHub"));
builder.Services.AddSignalR(options =>
{
    options.ClientTimeoutInterval = TimeSpan.FromMinutes(15);
});

builder.Services.AddSingleton<ChatHubService>();
builder.Services.AddSingleton<AiChatHub>();

var app = builder.Build();

// SignalR
app.MapHub<AiChatHub>("/chatHub");

app.Run();
