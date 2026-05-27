using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Backend.Models;

public class ScanHistoryEntry
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Source { get; set; } = string.Empty;
    public string CommitId { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsVulnerable { get; set; }
    public string VulnerabilityType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string OriginalCode { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
