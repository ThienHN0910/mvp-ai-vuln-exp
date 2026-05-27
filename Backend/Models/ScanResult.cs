namespace Backend.Models;

public class ScanResult
{
    public Guid Id { get; set; }
    public string CommitId { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsVulnerable { get; set; }
    public string VulnerabilityType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string OriginalCode { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }

    public static List<ScanResult> Results { get; } = new();
    public static object ResultsLock { get; } = new();
}
