# Quick Start Guide

## What This Does

Analyzes code for security vulnerabilities using:
1. **Regex + AST patterns** (fast, free)
2. **Elasticsearch search** (NLP-like similarity)
3. **Gemini API** (LLM fallback, rate-limited)
4. **Secrets detection** (hardcoded keys, credentials)
5. **Auto-learning** (saves confirmed issues to MongoDB)

## 30-Second Setup

### 1. Start services
```bash
docker-compose up -d
```

### 2. Build & run backend
```bash
cd Backend
dotnet run
```

### 3. Test a simple scan
```bash
curl -X POST http://localhost:5000/api/analyze \
  -H "Content-Type: application/json" \
  -d '{"rawCode":"var query = \"SELECT * FROM Users WHERE id = '\'\" + userId + \"'\'\";"}'
```

Expected: Returns vulnerability details for SQL Injection.

## Key Endpoints

| Endpoint | What It Does | Speed |
|----------|-------------|-------|
| `POST /api/ast/index` | Extract & index code patterns | Fast |
| `POST /api/secrets/detect` | Find hardcoded secrets | Very Fast |
| `POST /api/analyze` | Full vulnerability scan | 1-5s (may use LLM) |
| `GET /api/webhook/results` | View scan history | Instant |

## Configuration

Edit `Backend/appsettings.json`:

**Essential:**
```json
{
  "GEMINI_API_KEY": "your-gemini-key",
  "Elasticsearch": { "Url": "http://localhost:9200" },
  "MongoDB": { "ConnectionString": "mongodb://admin:password123@localhost:27017" }
}
```

## How It Works

1. **Input**: Code snippet or GitHub webhook
2. **Secrets scan**: Regex patterns detect hardcoded API keys, passwords
3. **AST extraction**: Parses code structure (Roslyn for C#)
4. **ES search**: Finds similar patterns in dataset
5. **Gemini fallback**: Only if ES doesn't find high-confidence match (rate-limited)
6. **Output**: Vulnerability report + suggested fix
7. **Learning**: If confirmed, auto-saved to MongoDB

## Limits

- **Gemini**: 15 requests/min, 500/day per IP
- **Code size**: Up to ~50KB per request (Gemini token limit)

## Where Are Things?

```
Backend/
  Services/
    AstExtractorService.cs       # Code parsing
    ElasticsearchService.cs      # Index & search
    MongoDbService.cs            # Dataset storage
    SecretsDetectService.cs      # Secret detection
    GeminiService.cs             # LLM analysis
  Controllers/
    AstController.cs             # /api/ast endpoints
    SecretsController.cs         # /api/secrets endpoints
    AnalyzeController.cs         # /api/analyze endpoint
  Middleware/
    GeminiRateLimitMiddleware.cs # Rate limiting

docker-compose.yml               # Elasticsearch + MongoDB
ARCHITECTURE.md                  # Full documentation
```

## Environment Variables

```bash
export GEMINI_API_KEY=your-key
export ELASTICSEARCH_URL=http://localhost:9200
export MONGODB_CONNECTION=mongodb://admin:password123@localhost:27017
```

## Stop Services

```bash
docker-compose down
```

## Need Help?

See [ARCHITECTURE.md](ARCHITECTURE.md) for:
- Full API reference
- Data schema (MongoDB, Elasticsearch)
- Troubleshooting
- Production deployment checklist
