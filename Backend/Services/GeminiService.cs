using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Backend.Models;

namespace Backend.Services;

public class GeminiService
{
    private static readonly Regex SqlInjectionRegex = new(
        "(?is)(?:\\$\"[^\"]*(?:SELECT|INSERT|UPDATE|DELETE)[^\"]*\\{[^}]+\\}[^\"]*\"|(?:SELECT|INSERT|UPDATE|DELETE)\\b[^\\n;]*\\+[^\\n;]*)",
        RegexOptions.Compiled);

    private static readonly Regex XssRegex = new(
        "(?is)(Html\\.Raw\\s*\\(|Response\\.Write\\s*\\(|<\\w+>\\s*\\+\\s*\\w+)",
        RegexOptions.Compiled);

    private static readonly Regex HardcodedSecretRegex = new(
        "(?is)(?:AIza[0-9A-Za-z\\-_]{20,}|(?:api[_-]?key|secret|token|password)\\s*=\\s*\"[^\"\\r\\n]{4,}\")",
        RegexOptions.Compiled);

    private static readonly Regex PathTraversalRegex = new(
        "(?is)(?:File\\.(?:ReadAllText|ReadAllBytes|OpenText|OpenRead)\\s*\\(\\s*[a-zA-Z_][a-zA-Z0-9_]*\\s*\\)|new\\s+FileStream\\s*\\(\\s*[a-zA-Z_][a-zA-Z0-9_]*\\s*,)",
        RegexOptions.Compiled);

    private static readonly Dictionary<string, string> VulnerabilityFixTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SQL Injection"] =
            "using var cmd = new SqlCommand(\"SELECT * FROM Users WHERE Name = @name\", connection);\ncmd.Parameters.Add(new SqlParameter(\"@name\", SqlDbType.NVarChar) { Value = input });",
        ["XSS"] =
            "using System.Text.Encodings.Web;\nvar safeOutput = HtmlEncoder.Default.Encode(input);\nreturn Results.Content(safeOutput, \"text/plain\");",
        ["Hardcoded Secret"] =
            "var apiKey = Environment.GetEnvironmentVariable(\"API_KEY\");\nif (string.IsNullOrWhiteSpace(apiKey)) throw new InvalidOperationException(\"Missing API key\");",
        ["Path Traversal"] =
            "var fileName = Path.GetFileName(userInput);\nvar safePath = Path.Combine(baseDirectory, fileName);\nvar content = File.ReadAllText(safePath);"
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiKey;

    public GeminiService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _apiKey = configuration["GEMINI_API_KEY"] ?? configuration["Gemini:ApiKey"] ?? string.Empty;
    }

    public async Task<ScanResult> AnalyzeCodeAsync(string rawCode, string? commitId = null, string? author = null, string? message = null)
    {
        var detectedType = DetectVulnerabilityType(rawCode, out var matchedTypes);

        if (detectedType is null)
        {
            return CreateResult(false, "None", "Low", "Mock AST pre-screen found no risky patterns.",
                "No remediation required.", rawCode, commitId, author, message);
        }

        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return CreateFallbackVulnerableResult(detectedType, rawCode, commitId, author, message,
                "GEMINI_API_KEY is not configured. Returned deterministic fallback analysis.");
        }

        try
        {
            var responseText = await CallGeminiAsync(rawCode, detectedType, matchedTypes);
            var parsed = ParseGeminiJson(responseText);

            return CreateResult(
                parsed.IsVulnerable,
                string.IsNullOrWhiteSpace(parsed.Type) ? detectedType : parsed.Type,
                string.IsNullOrWhiteSpace(parsed.Severity) ? DefaultSeverityFor(parsed.Type ?? detectedType) : parsed.Severity,
                parsed.Explanation,
                string.IsNullOrWhiteSpace(parsed.SuggestedFix)
                    ? VulnerabilityFixTemplates.GetValueOrDefault(parsed.Type ?? detectedType, "Use secure coding best practices.")
                    : parsed.SuggestedFix,
                rawCode,
                commitId,
                author,
                message);
        }
        catch (Exception ex)
        {
            return CreateFallbackVulnerableResult(detectedType, rawCode, commitId, author, message,
                $"Gemini request failed: {ex.Message}");
        }
    }

    private async Task<string> CallGeminiAsync(string rawCode, string detectedType, IReadOnlyCollection<string> matchedTypes)
    {
        var endpoint =
            $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={_apiKey}";

        var systemInstruction =
            "You are a senior C# application security scanner. Analyze code as a multi-vulnerability engine. " +
            "Focus first on AST-detected category hints and validate context. Return STRICT RAW JSON only, no markdown, no commentary. " +
            "Schema: {\"isVulnerable\":bool,\"type\":string,\"severity\":\"Critical|High|Medium|Low\",\"explanation\":string,\"suggestedFix\":string}. " +
            "Type must be one of: SQL Injection, XSS, Hardcoded Secret, Path Traversal. " +
            "Generate production-grade C# fix: SqlParameter for SQLi, HtmlEncoder for XSS, Environment.GetEnvironmentVariable for secrets, Path.GetFileName + allowlisted base path for traversal.";

        var userPrompt =
            $"Detected by AST pre-screen: {detectedType}. All matched categories: {string.Join(", ", matchedTypes)}.\n" +
            "Analyze this code and respond with raw JSON only:\n" + rawCode;

        var payload = new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new { text = systemInstruction }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = userPrompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.1,
                responseMimeType = "application/json"
            }
        };

        var json = JsonSerializer.Serialize(payload);
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Gemini API call failed with status {(int)response.StatusCode} ({response.StatusCode}). Body: {responseBody}");
        }

        using var rootDoc = JsonDocument.Parse(responseBody);
        var rawJson = rootDoc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(rawJson))
        {
            throw new InvalidOperationException("Gemini returned an empty body.");
        }

        return rawJson.Trim();
    }

    private static GeminiResponse ParseGeminiJson(string rawText)
    {
        var cleaned = rawText.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            cleaned = cleaned.Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("```", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();
        }

        var parsed = JsonSerializer.Deserialize<GeminiResponse>(cleaned, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        return parsed ?? throw new InvalidOperationException("Gemini JSON parse returned null.");
    }

    private static string? DetectVulnerabilityType(string rawCode, out List<string> matchedTypes)
    {
        matchedTypes = new List<string>();

        if (SqlInjectionRegex.IsMatch(rawCode))
        {
            matchedTypes.Add("SQL Injection");
        }

        if (XssRegex.IsMatch(rawCode))
        {
            matchedTypes.Add("XSS");
        }

        if (HardcodedSecretRegex.IsMatch(rawCode))
        {
            matchedTypes.Add("Hardcoded Secret");
        }

        if (PathTraversalRegex.IsMatch(rawCode))
        {
            matchedTypes.Add("Path Traversal");
        }

        return matchedTypes.FirstOrDefault();
    }

    private static ScanResult CreateFallbackVulnerableResult(string type, string rawCode, string? commitId, string? author,
        string? message, string explanation)
    {
        return CreateResult(
            true,
            type,
            DefaultSeverityFor(type),
            explanation,
            VulnerabilityFixTemplates.GetValueOrDefault(type, "Use secure coding best practices."),
            rawCode,
            commitId,
            author,
            message);
    }

    private static ScanResult CreateResult(bool isVulnerable, string type, string severity, string explanation,
        string suggestedFix, string rawCode, string? commitId, string? author, string? message)
    {
        return new ScanResult
        {
            Id = Guid.NewGuid(),
            CommitId = commitId ?? string.Empty,
            Author = author ?? string.Empty,
            Message = message ?? string.Empty,
            IsVulnerable = isVulnerable,
            VulnerabilityType = type,
            Severity = severity,
            Explanation = explanation,
            OriginalCode = rawCode,
            SuggestedFix = suggestedFix,
            Timestamp = DateTime.UtcNow
        };
    }

    private static string DefaultSeverityFor(string? type)
    {
        return type switch
        {
            "SQL Injection" => "Critical",
            "XSS" => "High",
            "Hardcoded Secret" => "High",
            "Path Traversal" => "High",
            _ => "Medium"
        };
    }

    private sealed class GeminiResponse
    {
        public bool IsVulnerable { get; set; }
        public string? Type { get; set; }
        public string? Severity { get; set; }
        public string Explanation { get; set; } = "Potential vulnerability detected.";
        public string SuggestedFix { get; set; } = string.Empty;
    }
}
