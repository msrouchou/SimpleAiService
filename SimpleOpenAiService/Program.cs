using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using SimpleOpenAiService;
using SimpleOpenAiService.Clients;
using SimpleOpenAiService.Configuration;
using SimpleOpenAiService.Hubs;

var builder = WebApplication.CreateBuilder(args);

ConfigureWorker(builder);

ConfigureSerilog(builder);

ConfigureAiClients(builder);

ConfigureSignalR(builder);

builder.Services.AddSwaggerGen();

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    })
    .AddMvcOptions(options =>
    {
        options.Filters.Add(new ProducesAttribute("application/json"));
    });

var app = builder.Build();

app.MapHub<AiChatHub>("/chatHub");

app.MapControllers();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;

        options.EnableTryItOutByDefault();
        options.DisplayRequestDuration();
        options.EnableDeepLinking();
    });

    // @ /api-docs
    app.UseReDoc();
}

app.Run();

#region LocalMethods

static void ConfigureSignalR(WebApplicationBuilder builder)
{
    builder.Services.Configure<ClientHubConfiguration>(builder.Configuration.GetSection("ClientHub"));
    builder.Services.AddSignalR(options =>
    {
        options.ClientTimeoutInterval = TimeSpan.FromMinutes(15);
    });

    builder.Services.AddSingleton<ChatHubService>();
    builder.Services.AddSingleton<AiChatHub>();
}

static void ConfigureAiClients(WebApplicationBuilder builder)
{
    // Ollama
    builder.Services.Configure<OllamaConfiguration>(builder.Configuration.GetSection("Ollama"));
    builder.Services.AddSingleton<OllamaClient>();
}

static void ConfigureSerilog(WebApplicationBuilder builder)
{
    builder.Host.UseSerilog((context, configuration) =>
        configuration.ReadFrom.Configuration(context.Configuration));
}

static void ConfigureWorker(WebApplicationBuilder builder)
{
    builder.Services.AddHostedService<Worker>();
}

#endregion