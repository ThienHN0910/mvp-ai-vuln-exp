using System.Threading.Tasks;
using MongoDB.Bson;
using MongoDB.Driver;
using Backend.Models;

namespace Backend.Services;

public class MongoDbService
{
    private readonly IMongoCollection<BsonDocument>? _collection;

    public MongoDbService(IConfiguration configuration)
    {
        var conn = configuration["MongoDB:ConnectionString"] ?? "mongodb://localhost:27017";
        var dbName = configuration["MongoDB:Database"] ?? "vuln_db";
        var coll = configuration["MongoDB:Collection"] ?? "dataset";
        var client = new MongoClient(conn);
        var db = client.GetDatabase(dbName);
        _collection = db.GetCollection<BsonDocument>(coll);
    }

    public async Task SaveScanResultAsync(ScanResult result)
    {
        if (_collection == null) return;
        var doc = new BsonDocument
        {
            { "Id", result.Id.ToString() },
            { "CommitId", result.CommitId },
            { "Author", result.Author },
            { "Message", result.Message },
            { "IsVulnerable", result.IsVulnerable },
            { "VulnerabilityType", result.VulnerabilityType },
            { "Severity", result.Severity },
            { "Explanation", result.Explanation },
            { "OriginalCode", result.OriginalCode },
            { "SuggestedFix", result.SuggestedFix },
            { "Timestamp", result.Timestamp }
        };

        await _collection.InsertOneAsync(doc);
    }
}
