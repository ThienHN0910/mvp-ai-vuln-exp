using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SecretsController : ControllerBase
{
    private readonly SecretsDetectService _secretsService;

    public SecretsController(SecretsDetectService secretsService)
    {
        _secretsService = secretsService;
    }

    [HttpPost("detect")]
    public async Task<IActionResult> DetectSecrets([FromBody] SecretsDetectRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "Code is required." });
        }

        try
        {
            // Use comprehensive detection: regex + git-secrets
            var secrets = await _secretsService.DetectSecretsComprehensiveAsync(request.Code, request.FilePath);

            if (secrets.Count == 0)
            {
                return Ok(new { message = "No secrets detected.", count = 0, secrets });
            }

            return Ok(new
            {
                message = $"Found {secrets.Count} potential secret(s).",
                count = secrets.Count,
                secrets = secrets.Select(s => new
                {
                    s.PatternName,
                    s.MatchedText,
                    s.Line,
                    s.Severity
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Secrets detection failed.", error = ex.Message });
        }
    }

    [HttpPost("detect-regex")]
    public IActionResult DetectSecretsRegex([FromBody] SecretsDetectRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "Code is required." });
        }

        try
        {
            var secrets = _secretsService.DetectSecrets(request.Code);

            return Ok(new
            {
                message = $"Found {secrets.Count} potential secret(s) via regex.",
                count = secrets.Count,
                secrets = secrets.Select(s => new
                {
                    s.PatternName,
                    s.MatchedText,
                    s.Line,
                    s.Severity
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Secrets detection failed.", error = ex.Message });
        }
    }
}

public class SecretsDetectRequest
{
    public string Code { get; set; } = string.Empty;
    public string? FilePath { get; set; }
}
