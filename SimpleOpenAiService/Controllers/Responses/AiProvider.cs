namespace SimpleOpenAiService.Controllers.Responses;

public record AiProvider(AiServerName Name, Uri Uri, string Model);
