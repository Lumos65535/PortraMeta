# PortraMeta

A local video metadata management tool that generates standard [Kodi-compatible NFO files](https://kodi.wiki/view/NFO_files/Movies) and portrait-oriented posters. Built for [Infuse](https://firecore.com/infuse), Kodi, Jellyfin, and other media players that read NFO metadata.

<!-- TODO: Add screenshot here -->
<!-- ![PortraMeta Screenshot](docs/screenshots/overview.png) -->

## Features

- **Full Kodi NFO Standard** — Read and write all standard [Kodi Movie NFO](https://kodi.wiki/view/NFO_files/Movies) fields: title, year, plot, studio, director, genre, runtime, content rating (MPAA), ratings, tags, unique IDs (IMDb/TMDB), actors, and more
- **Round-trip NFO Preservation** — When editing, unknown/custom XML elements in existing NFO files are preserved rather than stripped
- **Library Scanning** — Add media directories and scan for video files with automatic NFO/poster/fanart detection
- **Metadata Editing** — Edit all NFO fields per video with a clean detail page UI
- **Configurable Detail Page** — Choose which metadata fields to display and edit via Settings (grouped by priority tier)
- **Batch Editing** — Select multiple videos and update visible fields in one operation; editable fields stay in sync with your field visibility settings
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
   git clone https://github.com/Lumos65535/portrameta.git
   cd portrameta
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
dotnet run --project PortraMeta.Api    # http://localhost:5001

# Frontend (in a separate terminal)
cd frontend
npm install
npm run dev                          # http://localhost:3000
```

## Project Structure

```
portrameta/
├── backend/
│   ├── PortraMeta.Api/        # Web API controllers, Program.cs
│   ├── PortraMeta.Core/       # Interfaces, models, DTOs
│   ├── PortraMeta.Data/       # EF Core, entities, service implementations
│   └── PortraMeta.Tests/      # Unit tests (xUnit)
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

PortraMeta generates standard [Kodi Movie NFO](https://kodi.wiki/view/NFO_files/Movies) files, compatible with Infuse, Jellyfin, Emby, and Plex (with plugins).

### Supported Fields

| Category | Fields |
|----------|--------|
| Basic | title, originaltitle, sorttitle, year, premiered |
| Description | plot, outline, tagline |
| Classification | genre, mpaa, tag |
| Production | studio, director, credits (writer), country |
| Ratings | ratings (multi-source with votes), userrating, top250 |
| Identifiers | uniqueid (IMDb, TMDB, etc.) |
| Cast | actor (name, role, order) |
| Collection | set |
| Misc | runtime, dateadded |

For the complete NFO specification, see the [Kodi Wiki — NFO Files/Movies](https://kodi.wiki/view/NFO_files/Movies).

### File Naming Convention

All files are co-located with the video:

- `{video}.nfo` — metadata
- `{video}-poster.jpg` — portrait poster (3:4)
- `{video}-fanart.jpg` — landscape fanart (16:9, optional)

## Documentation

- [API Reference](docs/API.md) — Full endpoint documentation
- [Contributing Guide](docs/CONTRIBUTING.md) — Development setup and coding standards
- [Swagger UI](http://localhost:5000/swagger) — Interactive API explorer (when running)

## Architecture

PortraMeta treats **NFO files as the source of truth** for metadata. SQLite serves only as a fast index/cache for querying and filtering. All write operations persist to both the NFO file and the database. Rescanning a library re-syncs data from NFO files into SQLite.

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
