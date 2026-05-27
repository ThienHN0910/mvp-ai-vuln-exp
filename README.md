# AI-Powered Code Vulnerability Intelligence (MVP)

Lightweight, efficient vulnerability detection using **RAG+AST-first approach** with AI fallback.

## Key Features

✅ **AST-based pattern detection** (fast, no LLM cost)  
✅ **Elasticsearch indexing** for semantic search  
✅ **Secrets detection** (regex + git-secrets)  
✅ **Gemini fallback** (rate-limited: 15rpm/500rpd)  
✅ **Auto-learning** (saves confirmed issues to MongoDB)  
✅ **GitHub webhook** integration  
✅ **Rate limiting** middleware  

**Stack:**
- Backend: .NET 8 Web API
- Frontend: Vue 3 (Vite) + Tailwind CSS
- Database: MongoDB (dataset), Elasticsearch (patterns)
- AI: Gemini 1.5 Flash (fallback only)

---

## Quick Start

1. **Start infrastructure** (Elasticsearch + MongoDB):
   ```bash
   docker-compose up -d
   ```

2. **Run backend**:
   ```bash
   cd Backend
   dotnet restore
   dotnet run
   ```

3. **Test a scan**:
   ```bash
   curl -X POST http://localhost:5000/api/analyze \
     -H "Content-Type: application/json" \
     -d '{"rawCode":"var query = \"SELECT * FROM Users WHERE id = '\'\" + input + \"'\'\";"}'
   ```

**For full setup details**, see [QUICKSTART.md](QUICKSTART.md).

---

## Documentation

| Document | Purpose |
|----------|---------|
| **[QUICKSTART.md](QUICKSTART.md)** | 30-sec setup, key endpoints, quick reference |
| **[ARCHITECTURE.md](ARCHITECTURE.md)** | Full design, API docs, data schemas, troubleshooting |
| **[PENTEST_NOTES.md](PENTEST_NOTES.md)** | Pentest automation, OWASP coverage, CI/CD workflow |

---

## Project Structure

```
.
├── Backend/                      # ASP.NET Core 8 Web API
│   ├── Services/
│   │   ├── AstExtractorService.cs         # Roslyn code parsing
│   │   ├── ElasticsearchService.cs        # Index & search
│   │   ├── MongoDbService.cs              # Dataset persistence
│   │   ├── SecretsDetectService.cs        # Secret detection
│   │   └── GeminiService.cs               # LLM analysis
│   ├── Controllers/
│   │   ├── AstController.cs               # /api/ast
│   │   ├── SecretsController.cs           # /api/secrets
│   │   ├── AnalyzeController.cs           # /api/analyze
│   │   └── WebhookController.cs           # /api/webhook
│   ├── Middleware/
│   │   └── GeminiRateLimitMiddleware.cs    # Rate limiting
│   └── appsettings.json
├── Frontend/                      # Vue 3 + Vite
├── docker-compose.yml            # Local dev (ES + MongoDB)
├── QUICKSTART.md                 # Quick reference
├── ARCHITECTURE.md               # Full technical docs
└── PENTEST_NOTES.md             # Pentest automation
```

---

## API Endpoints

### Code Analysis

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/analyze` | `POST` | Direct code analysis (Gemini) |
| `/api/secrets/detect` | `POST` | Detect hardcoded secrets |
| `/api/ast/index` | `POST` | Extract & index to Elasticsearch |

### Webhook

| Endpoint | Method | Purpose |
|----------|--------|---------|
| `/api/webhook/github` | `POST` | GitHub push webhook |
| `/api/webhook/results` | `GET` | Scan history |

**See [ARCHITECTURE.md](ARCHITECTURE.md#api-usage-examples) for request/response examples.**

---

## Configuration

### Environment Variables (or appsettings.json)

```bash
# Required
export GEMINI_API_KEY="your-gemini-key"

# Optional (defaults shown)
export ELASTICSEARCH_URL="http://localhost:9200"
export MONGODB_CONNECTION="mongodb://admin:password123@localhost:27017"
```

### Docker Compose (Local Dev)

Default credentials:
- **Elasticsearch**: No auth (dev mode)
- **MongoDB**: `admin` / `password123`

Customize in `docker-compose.yml` before production.

---

## Backend Setup (Detailed)

```bash
cd Backend

# 1. Restore NuGet packages
dotnet restore

# 2. Build
dotnet build

# 3. Run
dotnet run
```

**Default URLs:**
- API: `http://localhost:5000` or `https://localhost:7001`
- Swagger: `http://localhost:5000/swagger` (if enabled)

**Prerequisites:**
- .NET SDK 8.0+
- Elasticsearch running on `http://localhost:9200`
- MongoDB running on `mongodb://localhost:27017`

---

## Frontend Setup (Detailed)

```bash
cd Frontend

# 1. Install dependencies
npm install
# or with pnpm:
pnpm install

# 2. Run dev server
npm run dev
# or
pnpm dev
```

**Default URL:** `http://localhost:5173`

**Build for production:**
```bash
npm run build
npm run preview
```

---

## Workflow: How Vulnerability Detection Works

```
Code Input
    ↓
[Secrets Scan] → Found critical secret? → Report immediately
    ↓ NO
[AST Extract] → Parse code structure (Roslyn)
    ↓
[Index to ES] → Store methods/nodes for search
    ↓
[ES Query] → Similar pattern in dataset?
    ↓ MATCH → High confidence finding
    ↓ NO MATCH
    ↓
[Gemini API] → LLM fallback (rate-limited)
    ↓
    Confirmed vulnerability? → YES → Save to MongoDB (auto-learn)
                               ↓ NO
                               → Cache as "verified safe"
```

---

## Rate Limiting

**Gemini API limits** (per client IP):
- 15 requests/minute
- 500 requests/day

When exceeded, API returns `429 Too Many Requests` with `Retry-After` header.

---

## Troubleshooting

**Elasticsearch not connecting?**
```bash
curl http://localhost:9200/_cluster/health
docker logs mvp-elasticsearch
```

**MongoDB connection error?**
```bash
docker ps | grep mongodb
docker logs mvp-mongodb
```

**Gemini API rate limited?**
Wait the time in `Retry-After` response header before retrying.

See [ARCHITECTURE.md#troubleshooting](ARCHITECTURE.md#troubleshooting) for more.

---

## Development Checklist

- [ ] Docker services running (`docker-compose up -d`)
- [ ] Backend builds successfully (`dotnet build`)
- [ ] Gemini API key set in `appsettings.json`
- [ ] ES index initialized via `POST /api/ast/ensure-index`
- [ ] Test scan returns results from `POST /api/analyze`

---

## Contributing

1. Clone repo
2. Create feature branch: `git checkout -b feature/my-feature`
3. Test locally with `docker-compose` + `dotnet run`
4. Commit & push
5. Open PR

See [PENTEST_NOTES.md](PENTEST_NOTES.md) for security testing guidelines.

---

## License

(Add license info here)

## Notes

- The AST pre-screen now still sends clean-looking code to Gemini for confirmation and caches snippets that Gemini verifies as safe.
- Webhook analysis runs asynchronously so GitHub gets an immediate 200 response.
- Results are kept in in-memory storage for MVP purposes.
