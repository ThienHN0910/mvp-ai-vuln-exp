using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/dataset")]
public class DatasetController(VulnerabilityScanner vulnerabilityScanner) : ControllerBase
{
    private readonly VulnerabilityScanner _vulnerabilityScanner = vulnerabilityScanner;

    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] AddRuleRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.VulnerabilityType) || string.IsNullOrWhiteSpace(request.RegexPattern))
        {
            return BadRequest(new { message = "VulnerabilityType and RegexPattern are required." });
        }

        var rule = new KnowledgeBaseRule
        {
            VulnerabilityType = request.VulnerabilityType,
            RegexPattern = request.RegexPattern,
            Severity = string.IsNullOrWhiteSpace(request.Severity) ? "Medium" : request.Severity,
            Explanation = request.Explanation ?? string.Empty,
            SuggestedFix = request.SuggestedFix ?? string.Empty
        };

        await _vulnerabilityScanner.AddKnowledgeBaseRuleAsync(rule);
        return Ok(rule);
    }

    [HttpGet("all")]
    public async Task<IActionResult> All()
    {
        var rules = await _vulnerabilityScanner.GetKnowledgeBaseRulesAsync();
        return Ok(rules);
    }
}

public class AddRuleRequest
{
    public string VulnerabilityType { get; set; } = string.Empty;
    public string RegexPattern { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string? Explanation { get; set; }
    public string? SuggestedFix { get; set; }
}
