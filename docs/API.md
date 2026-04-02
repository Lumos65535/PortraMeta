# API Reference

Base URL: `http://localhost:5000/api`

All responses follow a unified format:

```json
// Success
{ "data": <payload>, "success": true }

// Error
{ "error": "Error message", "success": false }
```

> Interactive API documentation is available at `/swagger` when the server is running.

## Authentication

API key authentication is **optional** and disabled by default. When enabled (by setting `Auth:ApiKey`), all requests must include the header:

```
X-Api-Key: your-secret-key
```

CORS preflight (`OPTIONS`) requests are exempt from authentication.

---

## Libraries

### List all libraries

```
GET /api/libraries
```

**Response:** `LibraryDto[]`

### Get a library

```
GET /api/libraries/{id}
```

### Create a library

```
POST /api/libraries
Content-Type: application/json

{
  "name": "My Library",
  "path": "/media/videos"
}
```

### Delete a library

```
DELETE /api/libraries/{id}
```

### Scan a library

Scans the library directory for video files and parses NFO metadata into the database.

```
POST /api/libraries/{id}/scan
```

**Response:**

```json
{
  "data": {
    "total": 150,
    "added": 10,
    "updated": 5,
    "skipped": 135,
    "nfoParsed": 15,
    "excludedFolders": 2
  },
  "success": true
}
```

### Get subdirectories

Returns immediate subdirectories of the library root path (for the excluded folder UI).

```
GET /api/libraries/{id}/subdirectories
```

### Get excluded folders

```
GET /api/libraries/{id}/excluded-folders
```

### Set excluded folders

Replaces the full set of excluded folder paths.

```
PUT /api/libraries/{id}/excluded-folders
Content-Type: application/json

{
  "paths": ["/media/videos/temp", "/media/videos/samples"]
}
```

---

## Videos

### List videos

```
GET /api/videos
```

**Query parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `page` | int | 1 | Page number |
| `page_size` | int | 50 | Items per page |
| `search` | string | | Search by filename or title |
| `has_nfo` | bool | | Filter by NFO existence |
| `has_poster` | bool | | Filter by poster existence |
| `library_id` | int | | Filter by library |
| `studio_id` | int | | Filter by studio |
| `sort_by` | string | | Sort field name |
| `sort_desc` | bool | false | Sort descending |

**Response:** `PagedResult<VideoFileDto>`

```json
{
  "data": {
    "items": [...],
    "totalCount": 150,
    "page": 1,
    "pageSize": 50
  },
  "success": true
}
```

### Get a video

```
GET /api/videos/{id}
```

**Response:** `VideoFileDto`

```json
{
  "data": {
    "id": 1,
    "libraryId": 1,
    "fileName": "example.mp4",
    "filePath": "/media/videos/example.mp4",
    "fileSizeBytes": 1073741824,
    "hasNfo": true,
    "hasPoster": true,
    "hasFanart": false,
    "title": "Example Video",
    "originalTitle": null,
    "year": 2024,
    "plot": "Description here.",
    "studioName": "Studio",
    "scannedAt": "2024-01-01T00:00:00Z",
    "actors": [
      { "id": 1, "name": "Actor Name", "role": "Role", "order": 0 }
    ]
  },
  "success": true
}
```

### Update a video

Updates metadata and writes the NFO file.

```
PUT /api/videos/{id}
Content-Type: application/json

{
  "title": "New Title",
  "originalTitle": "Original Title",
  "year": 2024,
  "plot": "Updated description.",
  "studioName": "Studio Name",
  "actors": [
    { "name": "Actor Name", "role": "Lead", "order": 0 }
  ]
}
```

All fields are optional — `null` fields are not modified.

### Batch update videos

```
PUT /api/videos/batch
Content-Type: application/json

{
  "ids": [1, 2, 3],
  "studioName": "Studio",
  "year": 2024
}
```

**Response:**

```json
{
  "data": { "updated": 3, "failed": [] },
  "success": true
}
```

### Batch delete videos

```
POST /api/videos/batch/delete
Content-Type: application/json

{
  "ids": [1, 2, 3],
  "mode": "Metadata"
}
```

Delete modes:
- `"Metadata"` — Delete NFO + poster + fanart files only
- `"Video"` — Delete the video file only
- `"All"` — Delete video file and all associated metadata files

**Response:**

```json
{
  "data": { "deleted": 3, "failed": [] },
  "success": true
}
```

---

## Images

### Get poster

```
GET /api/videos/{id}/poster
```

Returns the image file with appropriate `Content-Type` (`image/jpeg`, `image/png`, or `image/webp`).

### Upload poster

```
POST /api/videos/{id}/poster
Content-Type: multipart/form-data

file: <image file>
```

Max file size: 10 MB. Accepted types: JPEG, PNG, WebP.

### Import poster from path

```
POST /api/videos/{id}/poster/from-path
Content-Type: application/json

{
  "path": "/local/path/to/image.jpg"
}
```

### Get fanart

```
GET /api/videos/{id}/fanart
```

### Upload fanart

```
POST /api/videos/{id}/fanart
Content-Type: multipart/form-data

file: <image file>
```

### Import fanart from path

```
POST /api/videos/{id}/fanart/from-path
Content-Type: application/json

{
  "path": "/local/path/to/image.jpg"
}
```

---

## Data Types

### VideoFileDto

| Field | Type | Description |
|-------|------|-------------|
| `id` | int | Primary key |
| `libraryId` | int | Associated library ID |
| `fileName` | string | File name (with extension) |
| `filePath` | string | Full file path |
| `fileSizeBytes` | long | File size in bytes |
| `hasNfo` | bool | Whether an NFO file exists |
| `hasPoster` | bool | Whether a poster image exists |
| `hasFanart` | bool | Whether a fanart image exists |
| `title` | string? | Video title |
| `originalTitle` | string? | Original title |
| `year` | int? | Release year |
| `plot` | string? | Plot/description |
| `studioName` | string? | Studio name |
| `scannedAt` | datetime | When the file was last scanned |
| `fileModifiedAt` | datetime? | File system modification time |
| `actors` | ActorDto[] | Associated actors |

### ActorDto

| Field | Type | Description |
|-------|------|-------------|
| `id` | int | Primary key |
| `name` | string | Actor name |
| `role` | string? | Role in the video |
| `order` | int | Display order |

### LibraryDto

| Field | Type | Description |
|-------|------|-------------|
| `id` | int | Primary key |
| `name` | string | Library name |
| `path` | string | Directory path |
| `createdAt` | datetime | Creation timestamp |
