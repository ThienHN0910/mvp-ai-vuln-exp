using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalyzeController(VulnerabilityScanner vulnerabilityScanner) : ControllerBase
{
    private readonly VulnerabilityScanner _vulnerabilityScanner = vulnerabilityScanner;

    [HttpPost]
    public async Task<IActionResult> Analyze([FromBody] AnalyzeRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RawCode))
        {
            return BadRequest(new { message = "RawCode is required." });
        }

        var result = await _vulnerabilityScanner.AnalyzeCodeAsync(request.RawCode, "manual", message: request.Language);
        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<IActionResult> History()
    {
        var items = await _vulnerabilityScanner.GetScanHistoryAsync("manual", 100);
        return Ok(items);
    }
}

public class AnalyzeRequest
{
    public string RawCode { get; set; } = string.Empty;
    public string? Language { get; set; }
}
