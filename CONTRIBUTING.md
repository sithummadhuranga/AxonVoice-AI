# Contributing

Thank you for considering a contribution to AxonVoice AI. This document covers the development setup, coding standards, and the pull request process.

## Development Setup

### Prerequisites

- .NET 10 SDK
- Node.js 22 LTS
- Docker Desktop (for running infrastructure locally)
- An IDE with C# support (Visual Studio 2022, Rider, or VS Code with C# Dev Kit)

### First-Time Setup

```bash
# Clone and enter the repository
git clone https://github.com/sithummadhuranga/AxonVoice-AI.git
cd AxonVoice-AI

# Restore .NET dependencies
dotnet restore AxonVoiceAI.slnx

# Install frontend dependencies
cd frontend/dashboard && npm install
cd ../widget && npm install
cd ../..

# Copy environment template and fill in local values
cp .env.example .env

# Start infrastructure only (no application services)
docker compose -f infra/docker-compose.dev.yml up -d

# Run all .NET tests
dotnet test AxonVoiceAI.slnx

# Run widget unit tests
cd frontend/widget && npm test
```

### Database Migrations

Schema changes are managed through EF Core migrations in the owning service project and are committed to source control. The `AgentConfig`, `ConversationStore`, and `KnowledgeBase` services apply pending migrations automatically on startup through `Database.MigrateAsync()`, so local and containerized boots stay aligned with committed schema changes.

Do not add migration folders or model snapshots to `.gitignore`. If a model change requires a schema change, generate the migration in that service and commit the migration alongside the code change.

### Running Services Individually

Each backend service can run with `dotnet run` from its project directory. Set environment variables from `.env` before starting.

```bash
# Example — Agent Config service
cd src/Services/AgentConfig
dotnet run
```

The repository root is intentionally shared by `src/`, `tests/`, `frontend/`, `infra/`, and `docs/`. Keep `AxonVoiceAI.slnx` at the root so backend source and backend test projects stay in one solution while frontend packages remain separate Node-based workspaces.

## Coding Standards

All code in this repository is production code. The full standards are defined in [AGENTS.md](AGENTS.md). The most important rules:

- **No `any` in TypeScript.** Use `unknown` and narrow explicitly.
- **`CancellationToken` on every async I/O operation** in .NET — no exceptions.
- **`tenant_id` on every database query** — never rely on RLS alone.
- **Test before the implementation is complete** — xUnit for .NET, Vitest for TypeScript.
- **Keep frontend tests isolated from source directories** — widget tests belong under `frontend/widget/tests/`, not under `src/`.
- **No comments describing what the code does** — only why a non-obvious decision was made.

## Pull Request Process

1. Branch from `main` — use `feat/`, `fix/`, or `chore/` prefixes.
2. Write tests for any logic you introduce.
3. Run `dotnet test` and `npm test` locally — all tests must pass.
4. Run `dotnet build AxonVoiceAI.slnx` with zero warnings.
5. If your change affects a public interface between services, update `ARCHITECTURE.md`.
6. Open a pull request with a description of what changed and why.

## Commit Message Format

```
type(scope): short description

Longer explanation if needed. Describe why, not what.
```

Types: `feat`, `fix`, `docs`, `test`, `refactor`, `chore`

Scopes: `gateway`, `session-relay`, `agent-config`, `knowledge-base`, `conversation-store`, `shared`, `dashboard`, `widget`, `infra`
