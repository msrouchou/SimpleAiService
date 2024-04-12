using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SimpleOpenAiService.Configuration;
using SimpleOpenAiService.Controllers.Responses;

namespace SimpleOpenAiService.Controllers;

[Route("")]
public class HomeController : Controller
{
    [HttpGet("AiProviders")]
    public ActionResult<IReadOnlyCollection<AiProvider>> GetAiServers(
        [FromServices] IOptions<OllamaConfiguration> ollamaConfiguration)
    {
        var ollama = ollamaConfiguration.Value;
        var ollamaServer = new AiProvider(AiServerName.Ollama, ollama.Uri, ollama.Model);

        AiProvider[] response = [ollamaServer];

        return Ok(response);
    }
}
