using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyzeController : ControllerBase
{
    private readonly GeminiService _geminiService;

    public AnalyzeController(GeminiService geminiService)
    {
        _geminiService = geminiService;
    }

    [HttpPost]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RawCode))
        {
            return BadRequest(new { message = "RawCode is required." });
        }

        var result = await _geminiService.AnalyzeCodeAsync(request.RawCode);
        return Ok(result);
    }
}

public class AnalyzeRequest
{
    public string RawCode { get; set; } = string.Empty;
    public string? Language { get; set; }
}
