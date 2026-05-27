using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AstController : ControllerBase
{
    private readonly AstExtractorService _astExtractor;
    private readonly ElasticsearchService _esService;

    public AstController(AstExtractorService astExtractor, ElasticsearchService esService)
    {
        _astExtractor = astExtractor;
        _esService = esService;
    }

    [HttpPost("index")]
    public async Task<IActionResult> IndexCode([FromBody] AstIndexRequest request)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Code))
        {
            return BadRequest(new { message = "Code is required." });
        }

        try
        {
            // Ensure index exists
            await _esService.EnsureIndexAsync();

            // Extract AST documents
            var docs = _astExtractor.ExtractFromCode(request.Code, request.Path ?? "");

            // Index each document
            var indexed = new List<object>();
            foreach (var doc in docs)
            {
                await _esService.IndexAsync(doc);
                indexed.Add(new { doc.Id, doc.NodeKind, doc.Name });
            }

            return Ok(new { message = "Code indexed successfully.", count = indexed.Count, documents = indexed });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Indexing failed.", error = ex.Message });
        }
    }

    [HttpPost("ensure-index")]
    public async Task<IActionResult> EnsureIndex()
    {
        try
        {
            await _esService.EnsureIndexAsync();
            return Ok(new { message = "Elasticsearch index ensured." });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Failed to ensure index.", error = ex.Message });
        }
    }
}

public class AstIndexRequest
{
    public string Code { get; set; } = string.Empty;
    public string? Path { get; set; }
}
