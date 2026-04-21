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

### Supported Local Workflows

- Docker Compose is the supported full-stack local environment.
- The recommended local Docker flow uses the development override so the stack stays containerized while Ollama runs on the host.
- Direct `dotnet run` or IDE launches for backend services are supported for debugging, but secrets must come from .NET User Secrets or environment variables.
- `.env` is for Docker Compose only. It is not loaded by `dotnet run`.
- Internal Docker service addresses are not taken from `.env`; Compose wires PostgreSQL, Redis, Qdrant, and Ollama by service name.

### Running The Full Stack With Docker

```bash
# 1. Clone
git clone https://github.com/sithummadhuranga/AxonVoice-AI.git
cd AxonVoice-AI

# 2. Configure environment
cp .env.example .env
# Edit .env — set PLATFORM_MASTER_KEY, JWT_SIGNING_KEY, POSTGRES_PASSWORD,
# DATA_PROTECTION_CERTIFICATE_BASE64, and DATA_PROTECTION_CERTIFICATE_PASSWORD.
# Keep PLATFORM_BASE_URL=http://localhost for the base compose file.

# 3. Start the full stack with the host-Ollama development override
docker compose --env-file .env -f infra/docker-compose.yml -f infra/docker-compose.dev.yml up -d --build

# 4. Open the tenant dashboard through the gateway
open http://localhost:8080
```

The gateway is the only public entrypoint in Docker. The dashboard is proxied at `/`, the API stays under `/api/*`, the relay WebSocket is exposed at `/ws/session`, and the embeddable widget assets are served at `/widget/*`.

The backend containers now persist ASP.NET Core DataProtection keys in a shared Docker volume and require a password-protected PFX to encrypt those keys at rest. For local Docker runs, generate a self-signed development certificate or use an internal PKI-issued certificate, export it as a password-protected PFX, and base64-encode the file contents into `DATA_PROTECTION_CERTIFICATE_BASE64`.

If you want Ollama containerized instead of using the host installation, run the base compose file without `infra/docker-compose.dev.yml`.

### Direct Backend Debugging Without Docker Secrets

All backend entry projects share one local User Secrets store. Set the values once against any backend project, then run the service you want to debug from the host.

```bash
# Example: write shared backend secrets via the AgentConfig project
dotnet user-secrets set "JWT_SIGNING_KEY" "<base64-signing-key>" --project src/Services/AgentConfig/AxonVoiceAI.AgentConfig.csproj
dotnet user-secrets set "PLATFORM_MASTER_KEY" "<base64-master-key>" --project src/Services/AgentConfig/AxonVoiceAI.AgentConfig.csproj
dotnet user-secrets set "POSTGRES_CONNECTION_STRING" "Host=localhost;Port=5432;Database=voiceagent;Username=voiceagent;Password=change_me_in_production" --project src/Services/AgentConfig/AxonVoiceAI.AgentConfig.csproj
```

The committed `launchSettings.json` files provide non-secret localhost defaults for `PLATFORM_BASE_URL`, Redis, Ollama, Qdrant, and the internal service URLs. That keeps local debugging convenient without checking secrets into the repository.

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
