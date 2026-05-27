# AI-powered Code Vulnerability Intelligence (MVP)

A full MVP with:
- **Backend**: .NET 8 Web API
- **Frontend**: Vue 3 (Vite) + Tailwind CSS
- **AI Engine**: Gemini 1.5 Flash with a mock AST prescreener for SQL Injection, XSS, Hardcoded Secrets, and Path Traversal.

## Project Structure

- `/.env.example`
- `/webhook-payload-sample.json`
- `/Backend/*`
- `/Frontend/*`

## Backend Setup

```bash
cd Backend
export GEMINI_API_KEY="your_key_here"
dotnet restore
dotnet run
```

Default backend URL: `http://localhost:5000` (or the URL reported by ASP.NET runtime).

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
  - Body: `{ "rawCode": "...", "language": "csharp" }`
- `POST /api/webhook/github`
  - Accepts GitHub push payload (sample in `webhook-payload-sample.json`)
- `GET /api/webhook/results`
  - Returns in-memory analyzed webhook commit results

## Notes

- The AST pre-screen now still sends clean-looking code to Gemini for confirmation and caches snippets that Gemini verifies as safe.
- Webhook analysis runs asynchronously so GitHub gets an immediate 200 response.
- Results are kept in in-memory storage for MVP purposes.
