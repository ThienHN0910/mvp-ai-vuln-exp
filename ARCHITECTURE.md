# MVP AI Vulnerability Analyzer - Architecture & Setup

## Overview

This project implements a **lightweight, efficient vulnerability detection system** using a **RAG+AST-first approach** with AI fallback. Instead of relying entirely on LLMs, the system:

1. **Extracts AST** (Abstract Syntax Trees) from code using Roslyn
2. **Indexes AST** in Elasticsearch for fast pattern matching
3. **Detects secrets** via regex and git-secrets
4. **Queries Elasticsearch** for similar vulnerability patterns
5. **Falls back to Gemini** only when needed (rate-limited to 15rpm/500rpd)
6. **Saves confirmed vulnerabilities** to MongoDB for future learning

This reduces LLM costs and latency while improving accuracy over time.

---

## Architecture

### Components

#### **Backend Services** (ASP.NET Core 8)

| Service | Purpose |
|---------|---------|
| `AstExtractorService` | Parses C# code → extracts method nodes via Roslyn |
| `ElasticsearchService` | Indexes AST docs, performs similarity searches |
| `MongoDbService` | Persists confirmed ScanResult datasets |
| `SecretsDetectService` | Detects hardcoded secrets (regex + git-secrets) |
| `GeminiService` | LLM analysis (fallback, rate-limited) |
| `GeminiRateLimitMiddleware` | Enforces 15rpm/500rpd rate limits |

#### **Controllers**

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `POST /api/analyze` | `AnalyzeController` | Direct code analysis via Gemini |
| `POST /api/webhook/github` | `WebhookController` | GitHub webhook integration |
| `GET /api/webhook/results` | `WebhookController` | Fetch scan history |
| `POST /api/ast/index` | `AstController` | Extract & index code to ES |
| `POST /api/ast/ensure-index` | `AstController` | Initialize ES index |
| `POST /api/secrets/detect` | `SecretsController` | Detect secrets (comprehensive) |
| `POST /api/secrets/detect-regex` | `SecretsController` | Detect secrets (regex only) |

---

## Local Development Setup

### Prerequisites

- **Docker & Docker Compose** (for ES + MongoDB)
- **.NET SDK 8.0+** (for Backend)
- **Node.js 18+** + pnpm (for Frontend)

### Step 1: Start Infrastructure

```bash
cd /path/to/mvp-ai-vuln-exp
docker-compose up -d
```

Verify services are running:
```bash
curl http://localhost:9200/_cluster/health        # Elasticsearch
mongosh --authenticationDatabase admin -u admin -p password123 localhost:27017  # MongoDB
```

**Default Credentials:**
- **ES**: No auth (xpack.security=false in dev)
- **MongoDB**: admin / password123

### Step 2: Update appsettings.json

Ensure `Backend/appsettings.json` contains:

```json
{
  "Elasticsearch": {
    "Url": "http://localhost:9200",
    "Index": "ast-index"
  },
  "MongoDB": {
    "ConnectionString": "mongodb://admin:password123@localhost:27017",
    "Database": "vuln_db",
    "Collection": "dataset"
  },
  "AutoSave": {
    "SaveToMongoOnGeminiConfirm": true
  }
}
```

### Step 3: Run Backend

```bash
cd Backend
dotnet restore
dotnet run
```

Server runs on `https://localhost:7001` (or `http://localhost:5000` in dev).

### Step 4: Run Frontend (Optional)

```bash
cd Frontend
pnpm install
pnpm dev
```

Frontend on `http://localhost:5173`.

---

## API Usage Examples

### 1. Index Code to Elasticsearch

```bash
curl -X POST http://localhost:5000/api/ast/index \
  -H "Content-Type: application/json" \
  -d '{
    "code": "public class Example { public void Method() { var x = 5; } }",
    "path": "Example.cs"
  }'
```

**Response:**
```json
{
  "message": "Code indexed successfully.",
  "count": 1,
  "documents": [
    {
      "id": "guid...",
      "nodeKind": "Method",
      "name": "Method"
    }
  ]
}
```

### 2. Detect Secrets

```bash
curl -X POST http://localhost:5000/api/secrets/detect \
  -H "Content-Type: application/json" \
  -d '{
    "code": "var apiKey = \"AIzaSyAlF7favEJrvuBqF9-wNotL0EodDvg37Wk\";",
    "filePath": "config.cs"
  }'
```

**Response:**
```json
{
  "message": "Found 1 potential secret(s).",
  "count": 1,
  "secrets": [
    {
      "patternName": "GoogleApiKey",
      "matchedText": "AIzaSyAlF7favEJrvuBqF9-wNotL0EodDvg37Wk",
      "line": 1,
      "severity": "High"
    }
  ]
}
```

### 3. Analyze Code (Gemini Fallback)

```bash
curl -X POST http://localhost:5000/api/analyze \
  -H "Content-Type: application/json" \
  -d '{
    "rawCode": "var query = \"SELECT * FROM Users WHERE Name = '\'\" + input + \"'\'\";"
  }'
```

**Response:**
```json
{
  "id": "guid...",
  "isVulnerable": true,
  "vulnerabilityType": "SQL Injection",
  "severity": "Critical",
  "explanation": "User input is directly concatenated into SQL query...",
  "suggestedFix": "using var cmd = new SqlCommand(\"SELECT * FROM Users WHERE Name = @name\", connection);\n...",
  "timestamp": "2026-05-27T..."
}
```

### 4. GitHub Webhook

POST endpoint: `POST /api/webhook/github`

Example GitHub payload:
```json
{
  "head_commit": {
    "id": "abc123...",
    "author": { "name": "Dev" },
    "message": "Fix bug in auth"
  },
  "mock_unsafe_code": "var pwd = \"password123\";"
}
```

---

## Configuration

### `appsettings.json` Reference

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "GEMINI_API_KEY": "your-key-here",
  "Gemini": {
    "Model": "gemini-3.1-flash-lite"
  },
  "Elasticsearch": {
    "Url": "http://localhost:9200",
    "Index": "ast-index"
  },
  "MongoDB": {
    "ConnectionString": "mongodb://admin:password123@localhost:27017",
    "Database": "vuln_db",
    "Collection": "dataset"
  },
  "AutoSave": {
    "SaveToMongoOnGeminiConfirm": true
  }
}
```

### Secrets Detection Patterns

The `SecretsDetectService` includes regex patterns for:
- Google API Keys, AWS keys, GitHub tokens
- Private SSH keys, SSL certificates
- Database connection strings
- Hardcoded passwords, API secrets

### Rate Limiting

**Gemini API Rate Limits:**
- 15 requests per minute (per client IP)
- 500 requests per day (per client IP)

Exceeded limits return `429 Too Many Requests` with `Retry-After` header.

---

## Data Pipeline

### Flow: Code → Finding → Dataset

```
User Code
    ↓
[SecretsDetect] → Secrets found? YES → Create ScanResult (Critical)
    ↓ NO
[AST Extract] → Extract methods/nodes
    ↓
[Index to ES] → Store for similarity search
    ↓
[ES Query] → Similar patterns in dataset?
    ↓ MATCH
    ↓ → Create ScanResult (High confidence)
    ↓ NO MATCH
    ↓
[Gemini API] (Fallback) → LLM Analysis
    ↓
    Vulnerable? → YES → Save to MongoDB ← (AutoSave enabled)
                  ↓ NO
                  → Cache as "verified safe"
```

---

## MongoDB Dataset Schema

Confirmed vulnerabilities stored in `vuln_db.dataset`:

```json
{
  "_id": ObjectId("..."),
  "Id": "guid-string",
  "CommitId": "abc123...",
  "Author": "dev-name",
  "Message": "commit message",
  "IsVulnerable": true,
  "VulnerabilityType": "SQL Injection",
  "Severity": "Critical",
  "Explanation": "User input concatenated into SQL...",
  "OriginalCode": "var query = ...",
  "SuggestedFix": "Use parameterized queries...",
  "Timestamp": ISODate("2026-05-27T...")
}
```

**Queries:**
```javascript
// Count by vulnerability type
db.dataset.aggregate([{ $group: { _id: "$VulnerabilityType", count: { $sum: 1 } } }])

// Find all Critical issues
db.dataset.find({ Severity: "Critical" })

// Most recent 10 findings
db.dataset.find().sort({ Timestamp: -1 }).limit(10)
```

---

## Elasticsearch Index Schema

Index name: `ast-index`

Mapping:
```json
{
  "mappings": {
    "properties": {
      "id": { "type": "keyword" },
      "path": { "type": "keyword" },
      "nodeKind": { "type": "keyword" },
      "name": { "type": "text", "analyzer": "standard" },
      "text": { "type": "text", "analyzer": "standard" }
    }
  }
}
```

---

## Pentest & Compliance

### OWASP Top 10 Coverage (Planned)

- [ ] A01:2021 – Broken Access Control (TODO)
- [x] A02:2021 – Cryptographic Failures (Hardcoded secrets detection)
- [x] A03:2021 – Injection (SQL, XSS regex patterns)
- [ ] A04:2021 – Insecure Design (TODO)
- [ ] A05:2021 – Security Misconfiguration (TODO)
- [ ] A06:2021 – Vulnerable & Outdated Components (TODO)
- [ ] A07:2021 – Authentication Failures (TODO)
- [ ] A08:2021 – Software & Data Integrity Failures (TODO)
- [ ] A09:2021 – Logging & Monitoring Failures (TODO)
- [ ] A10:2021 – SSRF (TODO)

**Automation:** Will integrate OWASP ZAP or custom shell scripts (limited to top 10) in CI pipeline.

---

## Deployment

### Production Checklist

- [ ] Set `xpack.security.enabled=true` in Elasticsearch (enable auth)
- [ ] Use strong MongoDB credentials (not "admin/password123")
- [ ] Run Backend behind reverse proxy (Nginx/IIS) with HTTPS
- [ ] Enable GitHub webhook signature verification
- [ ] Set Gemini rate limits per team/project (not just IP)
- [ ] Monitor Elasticsearch disk usage (100+ GB growth possible)
- [ ] Backup MongoDB daily
- [ ] Use secrets manager (AWS Secrets, Azure Key Vault) for API keys
- [ ] Enable logging & alerting

---

## Troubleshooting

### Elasticsearch not connecting

```bash
# Check if running
curl http://localhost:9200

# View logs
docker logs mvp-elasticsearch

# Restart
docker restart mvp-elasticsearch
```

### MongoDB connection errors

```bash
# Test connection
mongosh --authenticationDatabase admin -u admin -p password123 localhost:27017

# Check running
docker ps | grep mongodb
```

### Gemini API rate limit hit

HTTP 429 response with `Retry-After` header. Wait specified seconds before retrying.

### High memory usage

Tune JVM for Elasticsearch in `docker-compose.yml`:
```yaml
environment:
  - "ES_JAVA_OPTS=-Xms1g -Xmx1g"  # Adjust as needed
```

---

## Contributing

1. Create feature branch: `git checkout -b feature/my-feature`
2. Test locally with docker-compose running
3. Build backend: `dotnet build`
4. Commit & push: `git push origin feature/my-feature`
5. Open PR with description

---

## License

(Add license info here)
