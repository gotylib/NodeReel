# NodeReel

Node-based video pipeline editor (n8n-style canvas) with a .NET main engine, PostgreSQL, MinIO, and FFmpeg.

## Features (v1)

- React Flow canvas: drag nodes from a server-driven catalog
- Auth: JWT login, admin creates users; each user has isolated pipelines
- Built-in nodes for video, image, and audio (upload, trim, resize, merge, …)
- External node servers: register a provider URL; main engine aggregates `GET /nodes` and proxies `POST /execute`
- Shared MinIO object keys between pipeline steps (no file bytes in JSON)

## Stack

| Layer | Tech |
|-------|------|
| API | .NET 10, onion (Api / Application / Domain / Infrastructure) |
| DB | PostgreSQL + EF Core |
| Files | MinIO (S3) |
| Video | FFmpeg |
| UI | React + Vite + TypeScript + `@xyflow/react` |

## Prerequisites

- Docker / Docker Compose
- .NET SDK 10
- Node.js 20+
- **FFmpeg** on `PATH` *or* Docker (fallback image `mwader/static-ffmpeg` is used automatically if `ffmpeg` is missing)

## Quick start

### 1. Infra

```bash
docker compose up -d
```

Starts Postgres (`localhost:5433`) and MinIO (`localhost:9100`, console `9101`).  
Credentials: `nodereel/nodereel` (Postgres), `minioadmin/minioadmin` (MinIO).

> Ports differ from the usual 5432/9000 so they do not clash with a local Postgres or an HTTP proxy on 9000. The MinIO .NET client disables system proxies for these calls.

### 2. API

```bash
cd backend
dotnet run --project NodeReel.Api --launch-profile http
```

API: http://localhost:5057 (Swagger in Development).

Migrations and MinIO bucket are applied on startup.

### 3. Frontend

```bash
cd frontend
npm install
npm run dev
```

UI: http://localhost:5173 (Vite proxies `/api` → `5057`).

Default admin: `admin` / `admin123` (see `Auth` in `appsettings.json`).

### 4. Optional sample custom node server

```bash
cd samples/CustomNodeServer
dotnet run
```

Listens on http://localhost:5088. In the UI open **Providers**, add:

- Name: `sample`
- URL: `http://localhost:5088`

Then **Refresh nodes** — you should see **Echo video**.

## Typical flow

1. Create/select a pipeline in the top bar, give it a name, click **Save**.
2. Add **Upload video** from the left panel (searchable, collapsible) → select a file in params.
3. Connect **Strip metadata** and/or **Invisible noise**.
4. Click **Run** → download the result from the status bar.

Saved pipelines are stored in Postgres (`/api/workflows`).

## External node server contract

Any custom server must implement:

### `GET /nodes`

Returns an array of descriptors:

```json
[
  {
    "id": "echo-video",
    "providerId": "custom",
    "name": "Echo video",
    "category": "sample",
    "description": "...",
    "inputs": [{ "name": "video", "type": "video", "required": true }],
    "outputs": [{ "name": "video", "type": "video" }],
    "paramsSchema": { "type": "object", "properties": {} }
  }
]
```

Main engine rewrites `providerId` to the registered provider GUID (hex / `N` format) when aggregating.

### `POST /execute`

Request:

```json
{
  "nodeId": "echo-video",
  "params": {},
  "inputs": { "video": "media/2026/01/01/....." }
}
```

Response:

```json
{
  "outputs": { "video": "media/2026/01/01/.....-out" },
  "logs": ["optional messages"]
}
```

`inputs` / `outputs` values are **MinIO object keys** in the shared `nodereel` bucket. The custom server should use the same MinIO credentials (see `samples/CustomNodeServer/appsettings.json`).

## Repo layout

```
NodeReel/
  docker-compose.yml
  backend/
    NodeReel.Api/
    NodeReel.Application/
    NodeReel.Domain/
    NodeReel.Infrastructure/
  frontend/
  samples/CustomNodeServer/
```

## Configuration

`backend/NodeReel.Api/appsettings.json`:

- `ConnectionStrings:Default`
- `Minio:*`
- `Ffmpeg:BinaryPath` / `Ffmpeg:TempDirectory`
- `Auth:*` (JWT key, seeded admin username/password)

## Notes

- Pipeline runs execute asynchronously in the API; the UI polls run status.
- Frontend renders a **generic** node UI from descriptors — ready for more server-defined nodes without UI redeploys.
