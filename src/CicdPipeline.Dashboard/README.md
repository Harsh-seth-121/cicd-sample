# CI/CD Pipeline Dashboard

A React-based frontend for the Temporal CI/CD Pipeline Orchestrator. Provides a real-time view of pipeline status, stage progression, and operator controls.

## Tech Stack

- **Vite 6** + **React 19** + **TypeScript**
- **Tailwind CSS 4** for styling
- **TanStack Query** for data fetching with automatic polling
- **React Router v7** for client-side routing
- **nginx** for production serving and API proxying

## Features

- **Pipeline List** — View all pipelines with status badges, filterable by repository and status. Auto-refreshes every 5 seconds.
- **Pipeline Detail** — Visual stage progression bar showing 11 pipeline stages. Displays failure evidence with diagnostic data.
- **Operator Controls** — Pause, cancel, and resume pipelines directly from the UI.
- **Trigger Pipeline** — Start a new pipeline via a form dialog.
- **Health Indicator** — Real-time API and Temporal health status in the header.

## Development Setup

### Prerequisites

- [Node.js 22+](https://nodejs.org/)
- The .NET API running at `http://localhost:5100` (see root README)

### Install and Run

```sh
cd src/CicdPipeline.Dashboard
npm install
npm run dev
```

The dev server starts at `http://localhost:3000` and proxies `/api` and `/health` requests to the .NET API at `http://localhost:5100`.

### Build for Production

```sh
npm run build
```

Output is written to `dist/` — static files served by nginx in Docker.

## Docker

The Dashboard is included in the project's `docker-compose.yml`:

```sh
docker-compose up --build
```

In Docker, nginx serves the SPA at port 3000 and proxies API requests to the `api` service.

## Environment / Configuration

| Setting | Dev (Vite) | Prod (nginx) |
|---------|-----------|--------------|
| Dashboard URL | `http://localhost:3000` | `http://localhost:3000` |
| API proxy target | `http://localhost:5100` | `http://api:8080` |

No environment variables are needed — the API proxy is configured in `vite.config.ts` (dev) and `nginx.conf` (prod).

## Pages

| Route | Description |
|-------|-------------|
| `/` | Pipeline list with filters and trigger button |
| `/pipelines/:workflowId` | Pipeline detail with stage progression, controls, and failure evidence |

## Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for component hierarchy, data flow diagrams, and state management details.
