using System.Text.RegularExpressions;

namespace Backend.Services;

public class SecretsDetectService
{
    /// <summary>
    /// Common regex patterns for detecting hardcoded secrets, API keys, credentials, etc.
    /// </summary>
    private static readonly Dictionary<string, Regex> SecretPatterns = new(StringComparer.OrdinalIgnoreCase)
    {
        // API Keys and tokens
        ["GoogleApiKey"] = new Regex(@"AIza[0-9A-Za-z\-_]{20,}", RegexOptions.Compiled),
        ["AwsApiKey"] = new Regex(@"AKIA[0-9A-Z]{16}", RegexOptions.Compiled),
        ["JwtToken"] = new Regex(@"eyJ[A-Za-z0-9_\-\.]+", RegexOptions.Compiled),
        ["PrivateKeyPem"] = new Regex(@"-----BEGIN (?:RSA |DSA |EC )?PRIVATE KEY-----", RegexOptions.Compiled),

        // Database connection strings
        ["MongoDbConnection"] = new Regex(@"mongodb[+]?://[^\s]+", RegexOptions.Compiled | RegexOptions.IgnoreCase),
        ["SqlConnectionString"] = new Regex(@"(server|data\s+source)\s*=\s*[^\s;]+.*(?:password|pwd)\s*=\s*[^\s;]+", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Email credentials  
        ["EmailPassword"] = new Regex(@"(?:email|smtp)(?:_?password|_?pwd)\s*[=:]\s*['""]?([^\s;'""]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // Generic secret patterns
        ["HardcodedApiKey"] = new Regex(@"(?:api[_-]?key|apikey|api_secret|access[_-]?token|secret[_-]?key|authorization[_-]?token)\s*[=:]\s*['""]([^\s;'""]+)['""]?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["HardcodedPassword"] = new Regex(@"(?:password|passwd|pwd|pass)\s*[=:]\s*['""]([^\s;'""]+)['""]", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["HardcodedSecret"] = new Regex(@"(?:secret|secret_key|secret_token)\s*[=:]\s*['""]([^\s;'""]+)['""]", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // AWS credentials
        ["AwsAccessKeyId"] = new Regex(@"(?:aws_access_key_id|access_key_id)\s*[=:]\s*['""]?([A-Z0-9]{20})['""]?", RegexOptions.IgnoreCase | RegexOptions.Compiled),
        ["AwsSecretAccessKey"] = new Regex(@"(?:aws_secret_access_key|secret_access_key)\s*[=:]\s*['""]?([A-Za-z0-9/+=]{40})['""]?", RegexOptions.IgnoreCase | RegexOptions.Compiled),

        // GitHub tokens
        ["GithubToken"] = new Regex(@"gh[pousr]_[A-Za-z0-9_]{36,255}", RegexOptions.Compiled),
        ["GithubPat"] = new Regex(@"github_pat_[A-Za-z0-9_]{22,255}", RegexOptions.Compiled),

        // Slack tokens
        ["SlackToken"] = new Regex(@"xox[baprs]-(?:\d+-){2,}[A-Za-z0-9_-]{20,}", RegexOptions.Compiled),

        // Firebase keys
        ["FirebaseKey"] = new Regex(@"AIza[0-9A-Za-z\-_]{35}", RegexOptions.Compiled),
    };

    public SecretsDetectService(IConfiguration configuration)
    {
        // Future: load custom patterns from config
    }

    /// <summary>
    /// Scan code for hardcoded secrets using regex patterns.
    /// </summary>
    public List<SecretMatch> DetectSecrets(string code)
    {
        var matches = new List<SecretMatch>();

        foreach (var (patternName, regex) in SecretPatterns)
        {
            var regexMatches = regex.Matches(code);
            foreach (Match match in regexMatches)
            {
                matches.Add(new SecretMatch
                {
                    PatternName = patternName,
                    MatchedText = match.Value,
                    Line = CountLinesToPosition(code, match.Index),
                    Severity = GetSeverityForPattern(patternName)
                });
            }
        }

        return matches;
    }

    /// <summary>
    /// Simulate git-secrets detection by calling external process.
    /// In production, integrate with actual git-secrets command or config.
    /// </summary>
    public async Task<List<SecretMatch>> DetectSecretsViaGitSecretsAsync(string filePath)
    {
        var matches = new List<SecretMatch>();

        try
        {
            // Example: invoke git secrets via ProcessStartInfo
            // This is a placeholder; actual implementation depends on git-secrets setup
            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = $"secrets scan {filePath}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = System.Diagnostics.Process.Start(processInfo);
            if (process is null) return matches;

            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                var stderr = await process.StandardError.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    // Parse git-secrets output and create matches
                    // Format typically: filename:line:secret_type
                    var lines = stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split(':');
                        if (parts.Length >= 3)
                        {
                            matches.Add(new SecretMatch
                            {
                                PatternName = "GitSecretsDetected",
                                MatchedText = line,
                                Line = int.TryParse(parts[1], out var lineNum) ? lineNum : 0,
                                Severity = "High"
                            });
                        }
                    }
                }
            }
        }
        catch (Exception)
        {
            // Log if needed; continue without blocking
        }

        return matches;
    }

    /// <summary>
    /// Combine both regex and git-secrets detection.
    /// </summary>
    public async Task<List<SecretMatch>> DetectSecretsComprehensiveAsync(string code, string? filePath = null)
    {
        var allMatches = new List<SecretMatch>();

        // First: regex-based detection
        allMatches.AddRange(DetectSecrets(code));

        // Second: git-secrets if file path provided
        if (!string.IsNullOrWhiteSpace(filePath))
        {
            allMatches.AddRange(await DetectSecretsViaGitSecretsAsync(filePath));
        }

        // Deduplicate by matched text
        var unique = allMatches
            .DistinctBy(m => m.MatchedText)
            .ToList();

        return unique;
    }

    private static int CountLinesToPosition(string text, int position)
    {
        var lineCount = 1;
        for (var i = 0; i < position && i < text.Length; i++)
        {
            if (text[i] == '\n')
                lineCount++;
        }
        return lineCount;
    }

    private static string GetSeverityForPattern(string patternName) => patternName switch
    {
        "PrivateKeyPem" => "Critical",
        "AwsApiKey" or "AwsAccessKeyId" or "AwsSecretAccessKey" => "Critical",
        "GithubToken" or "GithubPat" => "Critical",
        "SlackToken" => "Critical",
        "FirebaseKey" => "High",
        "GoogleApiKey" => "High",
        "JwtToken" => "High",
        "MongoDbConnection" or "SqlConnectionString" => "High",
        _ => "Medium"
    };

    public sealed class SecretMatch
    {
        public string PatternName { get; init; } = string.Empty;
        public string MatchedText { get; init; } = string.Empty;
        public int Line { get; init; }
        public string Severity { get; init; } = "Medium";
    }
}
