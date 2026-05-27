using System.Text.Json;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/webhook")]
public class WebhookController : ControllerBase
{
    private const string FallbackUnsafeCode = "var query = \"SELECT * FROM Users WHERE Name = '\" + input + \"'\";";
    private readonly GeminiService _geminiService;

    public WebhookController(GeminiService geminiService)
    {
        _geminiService = geminiService;
    }

    [HttpPost("github")]
    public IActionResult ReceiveGithubWebhook([FromBody] JsonElement payload)
    {
        var commitId = ExtractString(payload, "head_commit", "id")
                       ?? ExtractLastCommitProperty(payload, "id")
                       ?? Guid.NewGuid().ToString("N");

        var author = ExtractString(payload, "head_commit", "author", "name")
                     ?? ExtractLastCommitAuthor(payload)
                     ?? ExtractString(payload, "pusher", "name")
                     ?? "unknown";

        var message = ExtractString(payload, "head_commit", "message")
                      ?? ExtractLastCommitProperty(payload, "message")
                      ?? "No commit message";

        var rawCode = ExtractMockUnsafeCode(payload);

        _ = Task.Run(async () =>
        {
            var result = await _geminiService.AnalyzeCodeAsync(rawCode, commitId, author, message);
            lock (ScanResult.Results)
            {
                ScanResult.Results.Insert(0, result);
            }
        });

        return Ok(new { message = "Webhook accepted. Analysis queued." });
    }

    [HttpGet("results")]
    public IActionResult GetResults()
    {
        lock (ScanResult.Results)
        {
            return Ok(ScanResult.Results.ToList());
        }
    }

    private static string ExtractMockUnsafeCode(JsonElement payload)
    {
        var candidates = new[] { "mock_unsafe_code", "unsafe_code", "diff", "mockDiff" };
        foreach (var key in candidates)
        {
            if (payload.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var line = value.GetString();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line.TrimStart('+', ' ');
                }
            }
        }

        return FallbackUnsafeCode;
    }

    private static string? ExtractString(JsonElement element, params string[] path)
    {
        var current = element;
        foreach (var part in path)
        {
            if (!current.TryGetProperty(part, out current))
            {
                return null;
            }
        }

        return current.ValueKind == JsonValueKind.String ? current.GetString() : null;
    }

    private static string? ExtractLastCommitProperty(JsonElement payload, string property)
    {
        if (!payload.TryGetProperty("commits", out var commits) || commits.ValueKind != JsonValueKind.Array || commits.GetArrayLength() == 0)
        {
            return null;
        }

        var latest = commits[commits.GetArrayLength() - 1];
        return latest.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static string? ExtractLastCommitAuthor(JsonElement payload)
    {
        if (!payload.TryGetProperty("commits", out var commits) || commits.ValueKind != JsonValueKind.Array || commits.GetArrayLength() == 0)
        {
            return null;
        }

        var latest = commits[commits.GetArrayLength() - 1];
        return ExtractString(latest, "author", "name");
    }
}
