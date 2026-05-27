using System;
using System.Threading.Tasks;
using Nest;

namespace Backend.Services;

public class ElasticsearchService
{
    private readonly ElasticClient _client;
    private readonly string _index;

    public ElasticsearchService(IConfiguration configuration)
    {
        var url = configuration["Elasticsearch:Url"] ?? "http://localhost:9200";
        _index = configuration["Elasticsearch:Index"] ?? "ast-index";
        var settings = new ConnectionSettings(new Uri(url)).DefaultIndex(_index);
        _client = new ElasticClient(settings);
    }

    public async Task IndexAsync<T>(T doc, string? index = null) where T : class
    {
        var idx = index ?? _index;
        await _client.IndexDocumentAsync(doc);
    }

    public async Task EnsureIndexAsync()
    {
        var exists = await _client.Indices.ExistsAsync(_index);
        if (!exists.Exists)
        {
            await _client.Indices.CreateAsync(_index, c => c.Map(m => m.AutoMap()));
        }
    }
}
