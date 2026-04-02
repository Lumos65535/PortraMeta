# NfoForge

A local video metadata management tool that generates standard [Kodi-compatible NFO files](https://kodi.wiki/view/NFO_files/Movies) and portrait-oriented posters. Built for [Infuse](https://firecore.com/infuse), Kodi, Jellyfin, and other media players that read NFO metadata.

<!-- TODO: Add screenshot here -->
<!-- ![NfoForge Screenshot](docs/screenshots/overview.png) -->

## Features

- **Library Scanning** — Add media directories and scan for video files with automatic NFO/poster/fanart detection
- **NFO Read & Write** — Parse existing NFO files and generate standard Kodi Movie NFO format
- **Metadata Editing** — Edit title, year, studio, plot, original title, and actors per video
- **Batch Editing** — Select multiple videos and update shared fields (studio, year, etc.) in one operation
- **Batch Delete** — Remove metadata files, video files, or both for selected videos
- **Poster & Fanart Management** — Upload, drag-and-drop, paste from clipboard, or import from local path
- **Excluded Folders** — Configure subdirectories to skip during library scans
- **Internationalization** — Full English and Chinese UI with language switching
- **Light / Dark / System Theme** — Material UI theme with persistence
- **API Key Authentication** — Optional header-based API key for securing access
- **Swagger / OpenAPI** — Interactive API documentation at `/swagger`

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Backend | .NET 9 (ASP.NET Core Web API) |
| Frontend | React + TypeScript + Material UI |
| Database | SQLite (via EF Core) |
| Deployment | Docker + Docker Compose |

## Quick Start

### Docker Compose (Recommended)

1. Clone the repository:

   ```bash
   git clone https://github.com/Lumos65535/nfoforge.git
   cd nfoforge
   ```

2. Start the services:

   ```bash
   MEDIA_PATH=/path/to/your/videos docker compose up -d
   ```

3. Open the web UI at **http://localhost:3000**

The backend API is available at **http://localhost:5000** and Swagger docs at **http://localhost:5000/swagger**.

#### Environment Variables

| Variable | Default | Description |
|----------|---------|-------------|
| `MEDIA_PATH` | `./media` | Path to your video library (mounted read-only) |
| `DB_PATH` | `./data` | Path for SQLite database persistence |
| `VITE_API_URL` | `http://localhost:5000` | Backend URL used by the frontend at build time |
| `Cors__AllowedOrigins` | `http://localhost:3000` | Comma-separated allowed CORS origins |
| `Cors__AllowAnyOrigin` | `false` | Set `true` for desktop/local mode |
| `Auth__ApiKey` | _(empty)_ | Set a value to require `X-Api-Key` header on all requests |

### Local Development

**Prerequisites:** .NET 9 SDK, Node.js 18+

```bash
# Backend
cd backend
dotnet run --project NfoForge.Api    # http://localhost:5001

# Frontend (in a separate terminal)
cd frontend
npm install
npm run dev                          # http://localhost:3000
```

## Project Structure

```
nfoforge/
├── backend/
│   ├── NfoForge.Api/        # Web API controllers, Program.cs
│   ├── NfoForge.Core/       # Interfaces, models, DTOs
│   ├── NfoForge.Data/       # EF Core, entities, service implementations
│   └── NfoForge.Tests/      # Unit tests (xUnit)
├── frontend/
│   └── src/
│       ├── api/             # Axios API client
│       ├── contexts/        # React contexts (notifications, theme)
│       ├── i18n/            # Translations (en.json, zh.json)
│       └── pages/           # Page components
├── docs/                    # Developer documentation
├── docker-compose.yml
└── CLAUDE.md                # AI assistant context
```

## NFO Format

NfoForge generates standard Kodi Movie NFO files, compatible with Infuse, Jellyfin, Emby, and Plex (with plugins):

```xml
<movie>
  <title>Video Title</title>
  <originaltitle>Original Title</originaltitle>
  <year>2024</year>
  <plot>Description text.</plot>
  <studio>Studio Name</studio>
  <actor>
    <name>Actor Name</name>
    <role>Role</role>
    <order>0</order>
  </actor>
</movie>
```

File naming convention (all files co-located with the video):
- `video.nfo` — metadata
- `video-poster.jpg` — portrait poster (3:4)
- `video-fanart.jpg` — landscape fanart (16:9, optional)

## Documentation

- [API Reference](docs/API.md) — Full endpoint documentation
- [Contributing Guide](docs/CONTRIBUTING.md) — Development setup and coding standards
- [Swagger UI](http://localhost:5000/swagger) — Interactive API explorer (when running)

## Architecture

NfoForge treats **NFO files as the source of truth** for metadata. SQLite serves only as a fast index/cache for querying and filtering. All write operations persist to both the NFO file and the database. Rescanning a library re-syncs data from NFO files into SQLite.

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│   React UI   │────▶│  .NET API    │────▶│  NFO Files   │
│  (Material)  │◀────│  (REST)      │────▶│  (on disk)   │
└──────────────┘     └──────┬───────┘     └──────────────┘
                            │
                     ┌──────▼───────┐
                     │   SQLite     │
                     │  (index)     │
                     └──────────────┘
```

## License

[MIT](LICENSE)
