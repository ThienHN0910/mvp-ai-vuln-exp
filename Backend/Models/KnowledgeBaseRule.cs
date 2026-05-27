using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Backend.Models;

public class KnowledgeBaseRule
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string VulnerabilityType { get; set; } = string.Empty;
    public string RegexPattern { get; set; } = string.Empty;
    public string Severity { get; set; } = "Medium";
    public string Explanation { get; set; } = string.Empty;
    public string SuggestedFix { get; set; } = string.Empty;
}
