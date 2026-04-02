# PortraMeta

A local video metadata management tool that generates NFO files and portrait-oriented posters for Infuse users.

## Language Policy

**All commit messages, pull request titles and descriptions, code review comments, and any other project documentation must be written in English.**

## Tech Stack

- Backend: .NET 9 ASP.NET Core Web API
- Frontend: React + TypeScript + Material UI (MUI) + react-router-dom
- Database: SQLite (via EF Core)
- Deployment: Docker + Docker Compose

## Project Structure

```
portrameta/
├── backend/
│   ├── PortraMeta.Api/          # Web API entry point, Controllers, Program.cs
│   ├── PortraMeta.Core/         # Interface definitions (ILibraryService, IVideoService, INfoParser, INfoService), Models (Result<T>, PagedResult<T>), DTOs
│   ├── PortraMeta.Data/         # EF Core DbContext, Entities, Migrations, Service implementations, NfoParser, NfoService, FileSystemScanner
│   └── PortraMeta.Tests/        # xUnit unit tests (to be implemented)
├── frontend/
│   ├── src/
│   │   ├── api/               # axios client (client.ts), librariesApi, videosApi
│   │   ├── contexts/          # NotifyContext (global Snackbar notifications)
│   │   ├── i18n/              # react-i18next config (index.ts), translation files (zh.json, en.json)
│   │   ├── pages/             # LibrariesPage, VideosPage, VideoDetailPage, SettingsPage
│   │   └── App.tsx            # BrowserRouter + Layout + Routes
│   └── package.json
├── docker-compose.yml
└── CLAUDE.md
```

## Common Commands

```bash
# Backend
cd backend
dotnet run --project PortraMeta.Api          # Start API (port 5000)
dotnet test                                # Run tests
dotnet ef migrations add <Name> --project PortraMeta.Data --startup-project PortraMeta.Api
dotnet ef database update --project PortraMeta.Data --startup-project PortraMeta.Api

# Frontend
cd frontend
npm run dev                                # Start dev server (port 3000)
npm run build                              # Production build

# Docker
docker compose up --build                  # Build and start
docker compose down                        # Stop
```

## Architecture Principles

### NFO as the Source of Truth
- NFO files are the sole persistent source of metadata; Infuse reads them directly
- SQLite is an index cache for fast queries and filtering only
- All write operations must write to both the NFO file and SQLite (via `INfoService.WriteAsync`)
- Scan operations sync data from NFO files into SQLite (via `INfoParser.ParseAsync`)

### Layered Architecture
- Controllers handle routing and parameter validation only — no business logic
- All business logic lives in Core layer interfaces + Data layer Service implementations
- Data layer handles database operations only — no business rules
- Cross-layer direct calls are strictly forbidden (Controllers must not access DbContext directly)

### Coding Standards
- Use async/await throughout all I/O operations
- Inject services via interfaces (IVideoService, INfoService, etc.)
- Use the `Result<T>` pattern for error handling — do not use exceptions for business flow control
- Unified API response format: `{ data, error, success }`
- Global exception handler middleware as a fallback (`UseExceptionHandler`), returns unified 500 format
- Frontend errors displayed via `useNotify()` hook as Snackbar notifications
- axios response interceptor extracts the `error` field and rejects uniformly

### i18n Standards
- Frontend uses `react-i18next`; translation files are at `frontend/src/i18n/zh.json` (Chinese) and `en.json` (English)
- **All user-visible text must be read via `t()` — hardcoding Chinese or English strings in TSX is strictly forbidden**
- When adding new pages or components, first add the corresponding keys to both translation files, then use `useTranslation()` in the component
- Translation keys are grouped by page/module (`nav.*`, `common.*`, `videos.*`, `videoDetail.*`, `libraries.*`, `settings.*`)
- Dynamic content (strings with variables) uses i18next interpolation syntax: `t('key', { name: value })`, with `{{name}}` in the translation file
- Default language is Chinese; user language preference is persisted to `localStorage` (key: `portrameta_lang`)

### Logging Standards
- Inject `ILogger<T>` into all Services
- Log key operations at Information level (library created, library deleted, scan completed, NFO written)
- Log warnings at Warning level (resource not found)
- Log exceptions at Error level (including full Exception object)
- Log queries at Debug level (to avoid log pollution)

## Data Model (Core Entities)

- `Library`: Media library (scan directory configuration)
- `VideoFile`: Video file (core entity, associated with all metadata)
- `Studio`: Studio (includes Logo path; looked up or created by Name during scan)
- `Actor`: Actor (includes aliases, avatar, etc.; looked up or created by Name during scan)
- `VideoActor`: Video-Actor many-to-many association (includes Role, Order)
- `ExcludedFolder`: Scan-excluded directories (associated with Library, stores absolute paths)

## NFO Format

Kodi Movie NFO standard (Infuse-compatible), file naming: `{videofile}.nfo`

```xml
<movie>
  <title>...</title>
  <originaltitle>...</originaltitle>
  <year>2023</year>
  <plot>...</plot>
  <studio>...</studio>
  <actor>
    <name>...</name>
    <role>...</role>
    <order>0</order>
  </actor>
</movie>
```

Poster naming: `{videofile}-poster.jpg` (portrait)
Fanart naming: `{videofile}-fanart.jpg` (landscape, optional)

## API Standards

- Base path: `/api/`
- Pagination parameters: `?page=1&page_size=50`
- Filter parameters: `?has_nfo=false&has_poster=false&studio_id=1`
- Video update: `PUT /api/videos/{id}`, writes both SQLite + NFO file
- Batch video update: `PUT /api/videos/batch`, writes SQLite + NFO for each video
- Get subdirectories: `GET /api/libraries/{id}/subdirectories` (for excluded folder UI browsing)
- Excluded folder management: `GET /api/libraries/{id}/excluded-folders`, `PUT /api/libraries/{id}/excluded-folders`

## Frontend Routes

- `/` → redirect to `/videos`
- `/videos` → video file list (searchable, click row to navigate to detail)
- `/videos/:id` → video detail + edit (view/edit combined; saving writes NFO)
- `/libraries` → media library management (add, delete, scan)
- `/settings` → settings (language switching, etc.)

## Backend Configuration

### CORS Configuration

Supports multiple modes, controlled via `appsettings.json` or environment variables:

```json
"Cors": {
  "AllowedOrigins": "http://localhost:3000",  // comma-separated, supports multiple origins
  "AllowAnyOrigin": false                      // true = allow any origin (local/desktop mode)
}
```

| Scenario | Configuration |
|----------|---------------|
| Docker default web | `AllowedOrigins=http://localhost:3000` |
| Multiple frontend domains | `AllowedOrigins=http://a.com,https://b.com` |
| Tauri/Electron desktop client | `AllowAnyOrigin=true` |

> Legacy config key `Cors:AllowedOrigin` (singular) remains backwards-compatible.

### API Key Authentication (Optional)

Disabled by default. When set to a non-empty value, all API requests must include the `X-Api-Key` header:

```json
"Auth": {
  "ApiKey": ""   // empty string = disabled; set a key to enable
}
```

Enable via environment variable in Docker:

```yaml
environment:
  - Auth__ApiKey=your-secret-key
```

Frontend build parameter (`VITE_API_KEY`); when set, axios automatically attaches the request header:

```bash
VITE_API_KEY=your-secret-key npm run build
```

> CORS OPTIONS preflight requests are exempt from API Key validation, so browser cross-origin negotiation is unaffected.

## Multi-Platform Extension Strategy

The current backend architecture is ready for multi-platform access — **no backend code changes required**, only configuration adjustments per scenario:

```
              .NET REST API (unchanged)
                      │
      ┌───────────────┼───────────────┐
      │               │               │
   Browser         Docker Nginx   Desktop shell (Tauri)
   (done)          (done)         macOS / Windows
```

### Desktop Client (Recommended: Tauri)

- Tauri as the native window shell, embedding a WebView that loads the existing React frontend
- .NET backend launched as a Tauri sidecar subprocess locally
- Backend config: `Cors__AllowAnyOrigin=true` (desktop local mode)
- Same Tauri codebase supports both macOS and Windows packaging
- **No changes required to any frontend or backend code**

### Roadmap

| Phase | Status | Content |
|-------|--------|---------|
| 0 | ✅ Done | Docker + Web baseline |
| 1 | ✅ Done | CORS multi-origin + optional API Key |
| 2 | Pending | Tauri project + embedded backend subprocess (macOS) |
| 3 | Pending | Windows installer packaging (same Tauri codebase) |

## Current Development Status (2026-04-01)

Completed:
1. ✅ Project scaffolding (Docker + .NET + React running)
2. ✅ Library scan service (directory traversal, video/NFO/poster file detection)
3. ✅ VideoFile list API + frontend file list component (with search, pagination)
4. ✅ NFO read/parse (sync metadata, Studio, Actor into SQLite during scan)
5. ✅ Metadata editing + NFO generation (`PUT /api/videos/{id}` + VideoDetailPage)
6. ✅ Backend logging (ILogger), global exception handler middleware
7. ✅ Frontend notification system (useNotify/Snackbar), loading states, search debounce, react-router
8. ✅ Excluded folder feature (ExcludedFolder entity + LibraryService + frontend UI dialog)
9. ✅ Video list column configuration persistence (column visibility, column width auto-saved to localStorage)
10. ✅ Poster upload (`POST /api/videos/{id}/poster`) + preview (`GET /api/videos/{id}/poster`)
11. ✅ Fanart upload (`POST /api/videos/{id}/fanart`) + preview (`GET /api/videos/{id}/fanart`)
12. ✅ Actor editing UI (VideoDetailPage edit mode: add/remove/edit actors and roles)
13. ✅ Light/dark/system theme switching (ThemeModeContext + Settings page)
14. ✅ CORS multi-origin + optional API Key authentication (Phase 1 multi-platform groundwork)
15. ✅ Batch video editing (`PUT /api/videos/batch` + DataGrid checkbox selection + BatchEditDialog)

Pending:
16. Scraper interface stub `/api/scrapers` (not yet implemented)
17. Tauri desktop client packaging (macOS / Windows)

## Keyboard Shortcuts Plan (Pending)

Plan to add keyboard shortcut support in VideoDetailPage and globally to improve browsing efficiency.

### File Navigation
| Shortcut | Action |
|----------|--------|
| `←` / `[` | Go to previous file |
| `→` / `]` | Go to next file |
| `Backspace` / `Escape` | Return to video list |

### Edit Operations
| Shortcut | Action |
|----------|--------|
| `E` | Enter edit mode (when no input field is focused) |
| `Ctrl+S` | Save current edits |
| `Escape` | Cancel editing (while in edit mode) |

### Implementation Notes
- Register global shortcuts via `useEffect` + `window.addEventListener('keydown', ...)`
- Check whether current focus is in an input field (`event.target` is `INPUT`, `TEXTAREA`, `SELECT`, or `[contenteditable]`) — if so, do not trigger shortcuts
- Can be encapsulated as a custom hook `useKeyboardNav`, accepting `prevId`, `nextId`, and `editing` state as parameters
- Note: shortcut listeners must be re-registered when `editing` state changes (update deps array)

## Known Limitations

- `Actor.AvatarPath`, `Actor.Aliases`, `Studio.LogoPath` fields are defined in entities but not yet exposed in the API or frontend
- Search is case-sensitive (SQLite `LIKE` default behavior)
- Individual video files cannot be re-scanned independently (only the entire library can be scanned)

## Plugin System Plan (Pending)

### Design Goals

Keep the core project lightweight. Optional features that depend on heavy system components (e.g., video screenshot capture, scrapers) are provided as **plugins** that users can integrate on demand.

### Plugin Form (Direction TBD — requires further planning before implementation)

| Approach | Description |
|----------|-------------|
| Backend dynamic assembly loading | Plugin is a .NET DLL loaded at runtime by the backend |
| Independent sidecar process | Plugin is a standalone process communicating with the main backend via HTTP API |
| Frontend standalone page module | Plugin is a frontend page/component mounted to the router on demand |

### Planned Plugins

- **Poster Generation Plugin** (see section below): the first official reference plugin, demonstrating video screenshot + image compositing capability

### Extension Direction

Users and third-party developers can build additional plugins based on the plugin interface (e.g., scrapers, subtitle management, cover cropping, etc.).

---

## Future Extension Plans

### Poster Generation Plugin Plan

Will be implemented as a plugin, not built into the core project. Core logic: backend extracts video frames → frontend displays them for user selection → backend composites the poster.

#### Technology Choice: Backend FFmpeg CLI (Lightest Option)

| Approach | Description | Verdict |
|----------|-------------|---------|
| Frontend HTML5 `<video>` + Canvas | Requires streaming entire video to browser; 5 GB files are unacceptable | ❌ |
| Frontend ffmpeg.wasm | ~50 MB initial download + requires Nginx COOP/COEP headers + slow | ❌ |
| **Backend FFmpeg CLI** | Video is already on the server, zero transfer, supports all formats (MKV/MP4/AVI/TS, etc.) | ✅ |

#### User Interaction Flow

1. Click "Generate poster from video" button (only available when no poster exists)
2. Backend calls `ffmpeg` to uniformly extract 16 thumbnail frames within the valid video range
3. Frontend displays the 16 thumbnails in a 4×4 grid popup
4. User manually selects 2 frames (ordered: 1st on top, 2nd on bottom)
5. Backend re-extracts the 2 selected frames at full resolution, composites them vertically using `SixLabors.ImageSharp`
6. Crops/pads to standard **3:4** portrait ratio (e.g., 900×1200), saves as `{videofile}-poster.jpg`

#### API Draft

```
POST /api/videos/{id}/poster/frames
→ Returns 16 thumbnails (base64 or temporary URLs)

POST /api/videos/{id}/poster/generate
Body: { frameIndices: [3, 11] }
→ Composites and saves the poster, returns updated VideoFile
```

#### Dependency Additions

- Docker: `apt-get install -y ffmpeg` (~70–90 MB image increase)
- NuGet: `SixLabors.ImageSharp` (pure managed library, ~5 MB, no system dependencies)

### Batch File Editing Plan

**Description:** Select multiple files in the video list page and batch-assign certain NFO fields (e.g., set studio, year, tags uniformly across selected files), syncing writes to the corresponding NFO files and SQLite.

#### Current State

- Backend: no batch update endpoint; only `PUT /api/videos/{id}` (single record) — **now implemented as `PUT /api/videos/batch`**
- Frontend: DataGrid has `checkboxSelection` with batch edit button — **now implemented**
- `UpdateVideoRequest` DTO has all fields nullable (naturally supports "partial field update" semantics)

#### Implementation Route

**Backend**

1. `BatchUpdateVideoRequest` DTO containing:
   - `ids: int[]` (target video ID list)
   - Optional fields identical to `UpdateVideoRequest` (`null` = "do not modify this field")
2. `IVideoService` adds `BatchUpdateAsync(BatchUpdateVideoRequest request)` method
3. New endpoint `PUT /api/videos/batch`, calls NFO write per record, wrapped in per-record error handling
4. Returns `{ updated: int, failed: int[] }`; frontend refreshes list accordingly

**Frontend**

1. `VideosPage` DataGrid uses `checkboxSelection` + `disableRowSelectionOnClick` (row click = navigate, checkbox click = select)
2. When selected count > 0, toolbar shows "Batch Edit" button
3. `BatchEditDialog` pops up: shows number of files to be edited, provides field inputs (empty = no change), calls batch API on confirm
4. After completion, clears selection and refreshes list

#### API

```
PUT /api/videos/batch
Body: {
  ids: [1, 2, 3],
  studioName: "Acme",   // null = no change
  year: 2023,
  title: null,          // no change
  ...
}
Response: { data: { updated: 3, failed: [] }, success: true }
```

#### Notes

- The `actors` field is excluded from batch editing due to high risk of overwriting individual actor lists
- Batch operations are irreversible; the Dialog should clearly indicate the number of affected files

---

### Filename Smart Parsing Plan

**Description:** Parse video filenames into metadata fields (title, year, studio, actors, etc.) using user-defined regex or tokenization rules, pre-filling fields for user confirmation before writing to NFO.

#### Core Interaction Modes

1. **Template parsing** (recommended, user-friendly for non-technical users): user defines a template string with placeholders, e.g.:
   ```
   {studio} {title} ({year})
   ```
   System converts the template to regex and extracts corresponding groups from the filename.

2. **Custom regex** (advanced mode): user inputs regex directly, using named capture groups (`(?P<title>...)`) to map fields.

3. **Delimiter tokenization** (simplest mode): tokenize by space/underscore/hyphen, user manually drags each token to the corresponding field.

#### Implementation Route

**Backend**

1. New `POST /api/videos/parse-filename` endpoint (**pure computation, no writes**):
   - Accepts `{ pattern: string, patternType: "template"|"regex"|"split", fileNames: string[] }`
   - Returns parse results per filename: `{ fileName, parsed: { title?, year?, studioName?, ... } }`
2. Template-to-regex conversion logic lives on the backend (C# `Regex` library), avoiding browser regex differences

**Frontend**

1. Add "Fill from Filename" entry in the "Batch Edit" toolbar on the video list page
2. User workflow in `FilenameParserDialog`:
   - Select parse mode (template / regex / tokenize)
   - Enter rule, live-preview parse results for the first 5 filenames on current page (calls backend preview API)
   - On confirm, batch pre-fills parsed results into each file's draft metadata
3. Preview phase writes no data; user clicks "Apply and Save" to call the batch update API

#### Template Placeholder Design

| Placeholder | Mapped Field | Example |
|-------------|--------------|---------|
| `{title}` | Title | `Inception` |
| `{originaltitle}` | Original title | `インセプション` |
| `{year}` | Year (4-digit) | `2010` |
| `{studio}` | Studio | `Warner` |
| `{actor}` | Actor (comma-separated) | `DiCaprio` |
| `{ignore}` | Ignore this segment | — |

#### API Draft

```
POST /api/videos/parse-filename
Body: {
  pattern: "{studio} {title} ({year})",
  patternType: "template",
  fileNames: ["Warner Inception (2010).mkv", "..."]
}
Response: {
  data: [
    { fileName: "Warner Inception (2010).mkv",
      parsed: { studio: "Warner", title: "Inception", year: 2010 } },
    ...
  ]
}
```

#### Notes

- Parse results may be ambiguous (e.g., filenames with multiple parentheses); the preview UI should allow manual correction of individual results
- Regex mode should validate for safety to avoid ReDoS
- Filename parsing is an assistive tool; only the user-confirmed batch save actually writes data

---

### VideoDetailPage — FileInfo Section Extension
The current FileInfo section only displays path, size, scan time, and NFO/Poster/Fanart status.
Planned additions (requires backend to parse and store during scan):
- Resolution (e.g., 1920×1080)
- Video codec (e.g., H.264, HEVC)
- Audio codec (e.g., AAC, DTS)
- Frame rate, duration, and other technical parameters
