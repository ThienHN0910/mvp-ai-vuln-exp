# Local Vulnerability Intelligence MVP

A 100% local lightweight MVP with:
- **Backend**: .NET 8 Web API + MongoDB.Driver
- **Frontend**: Vue 3 (Vite) + Tailwind CSS
- **Engine**: AST/Regex pattern matching with a MongoDB knowledge base

## Project Structure

- `/.env.example`
- `/webhook-payload-sample.json`
- `/Backend/*`
- `/Frontend/*`

## Requirements

- .NET 8 SDK
- Node.js 18+
- MongoDB running locally at `mongodb://localhost:27017`

## Backend Setup

```bash
cd Backend
dotnet restore
dotnet run
```

Default backend URL: `http://localhost:5000` (or the ASP.NET runtime URL).

### MongoDB Configuration

`Backend/appsettings.json` defaults to:

```json
{
  "MongoDb": {
    "ConnectionString": "mongodb://localhost:27017",
    "DatabaseName": "VulnerabilityDB",
    "KnowledgeBaseCollection": "KnowledgeBase",
    "ScanHistoryCollection": "ScanHistory"
  }
}
```

## Frontend Setup

```bash
cd Frontend
npm install
npm run dev
```

Default frontend URL: `http://localhost:5173`.

Set API URL if needed:

```bash
VITE_API_BASE_URL=http://localhost:5000
```

## API Endpoints

- `POST /api/analyze`
- `GET /api/analyze/history`
- `POST /api/webhook/github`
- `GET /api/webhook/results`
- `POST /api/dataset/add`
- `GET /api/dataset/all`
- `POST /api/pentest/run`

## Notes

- All scans are performed locally using regex/AST-style matching rules from MongoDB.
- `KnowledgeBase` stores vulnerability rules.
- `ScanHistory` stores logs from manual scans and webhook scans.
- Hardcoded secret detection includes strict matching for patterns like `api_key = "..."` and `password = "..."`.
