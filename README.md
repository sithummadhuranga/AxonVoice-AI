# AxonVoice AI

[![CI](https://github.com/sithummadhuranga/AxonVoice-AI/actions/workflows/ci.yml/badge.svg)](https://github.com/sithummadhuranga/AxonVoice-AI/actions/workflows/ci.yml)

A production-grade, self-hostable, multi-tenant real-time voice agent platform. Businesses deploy AI agents that handle live voice calls in Sinhala, Tamil, and English, with RAG-backed knowledge bases and structured booking workflows.

## Stack

| Layer | Technology |
|-------|-----------|
| Backend services | .NET 10 (ASP.NET Core) |
| API gateway | YARP (Yet Another Reverse Proxy) |
| Frontend dashboard | Next.js 16 + TypeScript |
| Embeddable widget | Vite + TypeScript |
| Primary database | PostgreSQL 16 |
| Cache / pub-sub | Redis 7 |
| Vector database | Qdrant |
| Local LLM runtime | Ollama (`nomic-embed-text`, `llama3.2`) |
| Live voice AI | Google Gemini Live API (tenant BYOK) |
| Tool orchestration | Microsoft Semantic Kernel |

## Quick Start

### Prerequisites

- Docker Desktop (or Docker Engine + Compose plugin)
- 8 GB RAM minimum (16 GB recommended)
- A Google Gemini API key (per tenant — see [BYOK model](#byok-model))

### Running Locally

```bash
# 1. Clone
git clone https://github.com/sithummadhuranga/AxonVoice-AI.git
cd AxonVoice-AI

# 2. Configure environment
cp .env.example .env
# Edit .env — set PLATFORM_MASTER_KEY, JWT_SIGNING_KEY, POSTGRES_PASSWORD at minimum

# 3. Start the full stack
docker compose -f infra/docker-compose.yml up -d

# 4. Wait for Ollama model downloads (first run only — ~2–5 minutes)
docker compose -f infra/docker-compose.yml logs -f ollama-init

# 5. Open the tenant dashboard
open http://localhost:3000
```

## Continuous Integration

GitHub Actions validates the repository on every push to `main` and on every pull request. The workflow runs:

- .NET restore, build, and xUnit tests for `AxonVoiceAI.slnx`
- dashboard dependency install, lint, and production build
- widget dependency install, Vitest suite, and production build

You can mirror the same checks locally with:

```bash
dotnet test AxonVoiceAI.slnx
cd frontend/dashboard && npm ci && npm run lint && npm run build
cd ../widget && npm ci && npm test && npm run build
```

## Database Migrations

The PostgreSQL schema is managed by EF Core code-first migrations committed under each service's `Data/Migrations` folder. `AgentConfig`, `ConversationStore`, and `KnowledgeBase` each call `Database.MigrateAsync()` during startup, so pending migrations are applied automatically when those services boot against a reachable database.

Best practice in this repository:

- commit EF Core migration files and model snapshots
- do not add migration folders to `.gitignore`
- treat `infra/migrations/V001__initial_schema.reference.sql` as a reference artifact, not the authoritative schema runner

If you add or change a persistence model, generate the matching EF Core migration in the owning service and commit it with the code change.

## BYOK Model

This platform does **not** pay for AI on behalf of tenants. Each tenant supplies their own Google Gemini API key through the dashboard. The key is stored AES-256 encrypted and used only during live voice sessions — never logged, never exposed.

## Documentation

- [Architecture & Engineering Specification](ARCHITECTURE.md)
- [Self-Hosting Guide](docs/SELF_HOSTING.md)
- [API Reference](docs/API_REFERENCE.md)
- [Widget Integration Guide](docs/WIDGET_INTEGRATION.md)

## Repository Structure

```
AxonVoiceAI.slnx            Root .NET solution spanning backend source and tests

src/
  Gateway/              YARP gateway — JWT auth, rate limiting, WebSocket routing
  Services/
    SessionRelay/       Real-time WebSocket proxy to Gemini Live API
    AgentConfig/        Tenant and agent CRUD, session token issuance
    KnowledgeBase/      Document ingestion, embedding, RAG retrieval
    ConversationStore/  Session recording, Ollama summarisation, webhooks
  Shared/               Contracts, DTOs, Semantic Kernel plugins

frontend/
  dashboard/            Next.js 16 tenant admin dashboard
  widget/               Vite embeddable <script> bundle
    tests/              Vitest unit tests for widget state, timers, and connection flow

infra/
  docker-compose.yml    Full production stack
  migrations/           PostgreSQL schema migrations

tests/                  xUnit test projects (one per service)
```

## Layout Notes

- The repository root is the product boundary. Backend source, backend tests, frontend packages, infrastructure, and documentation are all first-class parts of the same deliverable.
- `AxonVoiceAI.slnx` stays at the repository root because it spans both `src/` and `tests/`. Moving it under a backend-only folder would make solution paths, Docker build contexts, and contributor commands less clear rather than more consistent.
- `frontend/` remains a root peer to `src/` because the dashboard and widget are deployable application packages, not backend implementation details.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for development setup, coding standards, and the pull request process.

## License

Apache 2.0 — see [LICENSE](LICENSE).
