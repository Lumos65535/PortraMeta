# Contributing to PortraMeta

Thank you for your interest in contributing! This guide covers the development setup and coding standards.

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Node.js 18+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (optional, for containerized runs)

## Development Setup

```bash
git clone https://github.com/Lumos65535/portrameta.git
cd portrameta
```

### Backend

```bash
cd backend
dotnet restore
dotnet run --project PortraMeta.Api    # Starts on http://localhost:5001
```

The API includes Swagger UI at `http://localhost:5001/swagger` for interactive testing.

### Frontend

```bash
cd frontend
npm install
npm run dev                          # Starts on http://localhost:3000
```

### Docker

```bash
MEDIA_PATH=/path/to/videos docker compose up --build
```

## Project Architecture

```
backend/
├── PortraMeta.Api/      # Controllers + Program.cs (routing, middleware)
├── PortraMeta.Core/     # Interfaces, DTOs, models (no implementation)
├── PortraMeta.Data/     # EF Core DbContext, entities, service implementations
└── PortraMeta.Tests/    # xUnit tests

frontend/src/
├── api/               # Axios client and API functions
├── contexts/          # React contexts (notifications, theme)
├── i18n/              # Translation files (en.json, zh.json)
└── pages/             # Page components
```

### Layered Architecture

- **Controllers** handle routing and parameter validation only — no business logic
- **Core** defines interfaces (`IVideoService`, `INfoService`, etc.) and data models
- **Data** implements services and database operations
- Controllers must not access `DbContext` directly

### Key Design Principles

- **NFO as source of truth** — SQLite is an index cache. All write operations must persist to both the NFO file and the database.
- **Result pattern** — Use `Result<T>` for error handling; do not throw exceptions for business flow control.
- **Unified API responses** — All endpoints return `{ data, error, success }`.
- **Async everywhere** — All I/O operations use `async/await`.

## Coding Standards

### Language Policy

**All code, comments, commit messages, PR descriptions, and documentation must be in English.**

### Backend (.NET)

- Inject services via interfaces (`IVideoService`, not `VideoService`)
- Use `ILogger<T>` for logging:
  - `Information` — key operations (scan completed, NFO written)
  - `Warning` — resource not found
  - `Error` — exceptions (include the full `Exception` object)
  - `Debug` — query details
- Follow the existing `Result<T>` pattern for service return types

### Frontend (React + TypeScript)

- All user-visible text must use `t()` from `react-i18next` — no hardcoded strings
- Add translation keys to **both** `en.json` and `zh.json`
- Translation keys are grouped by page/module: `nav.*`, `common.*`, `videos.*`, `videoDetail.*`, `libraries.*`, `settings.*`
- Use `useNotify()` hook for toast notifications
- Display errors via the Snackbar notification system

## Running Tests

```bash
# Backend
cd backend
dotnet test

# Frontend
cd frontend
npm test
```

## Database Migrations

When modifying entities:

```bash
cd backend
dotnet ef migrations add <MigrationName> \
  --project PortraMeta.Data \
  --startup-project PortraMeta.Api
```

Migrations are applied automatically on startup.

## Pull Request Guidelines

1. Create a feature branch from `main`
2. Keep PRs focused — one feature or fix per PR
3. Include a clear description of what changed and why
4. Ensure `dotnet build` and `dotnet test` pass
5. Ensure `npm run build` passes (no TypeScript errors)
6. Update translation files if adding user-visible text
7. Update API documentation if adding/changing endpoints

## Reporting Issues

Please open an issue on GitHub with:
- Steps to reproduce
- Expected vs actual behavior
- Environment details (OS, Docker version, browser)
