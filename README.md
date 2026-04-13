# Temporal Cloud CI/CD Pipeline Orchestrator

A CI/CD pipeline orchestrator built with .NET 8 and the Temporal .NET SDK. Temporal Cloud handles workflow orchestration, history, and visibility while dedicated .NET workers execute build, test, version, publish, and deploy stages.

## Project Overview

### Architecture

The system is composed of:

- **4 Workflows** — `PipelineIngressWorkflow`, `BuildValidationWorkflow`, `VersionAndPublishWorkflow`, `DeploymentWorkflow`
- **5 Activity classes** — Ingress, Build/Test, GitVersion, Publish, Deploy
- **ASP.NET Core API** — Webhook intake, operator controls, and health checks
- **5 Workers** — Each on a dedicated task queue for isolated scaling and failure containment

### Pipeline Flow

```
Webhook / Manual / Scheduled trigger
  -> PipelineIngressWorkflow   (validate, normalize, deduplicate)
  -> BuildValidationWorkflow   (checkout, build, test, scan)
  -> VersionAndPublishWorkflow (GitVersion, image build, registry push, manifest)
  -> DeploymentWorkflow        (deploy to DEV; if branch == main, also deploy to QA)
```

All branches deploy to **DEV**. Only `main` progresses from DEV to **QA**. The same published image digest is promoted across environments.

### Task Queues

| Queue | Worker | Purpose |
|-------|--------|---------|
| `cicd.pipeline.orchestrator` | Orchestrator | All 4 workflows + ingress activities |
| `cicd.build.test` | BuildTest | Build, test, and scan activities |
| `cicd.gitversion` | GitVersion | Semantic version computation |
| `cicd.publish` | Publish | Image push and manifest creation |
| `cicd.deploy` | Deploy | Environment deployment and verification |

### Tech Stack

- .NET 8 / C# with central package management
- Temporal .NET SDK 1.13.0
- Docker + Docker Compose
- GitHub Actions CI/CD
- GitVersion for semantic versioning

## Build, Test, and Run

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker](https://docs.docker.com/get-docker/) (for Docker Compose option)
- [Temporal CLI](https://docs.temporal.io/cli) (for local dev server option)

### Build

```sh
dotnet build
```

### Test

```sh
dotnet test
```

### Format

```sh
dotnet format
```

### Run Locally

#### Option A: Local Temporal Dev Server

Start Temporal and register the namespace and search attributes:

```sh
./scripts/start-local.sh
```

Then, in separate terminals:

```sh
dotnet run --project src/CicdPipeline.Api
dotnet run --project src/CicdPipeline.Worker.Orchestrator
dotnet run --project src/CicdPipeline.Worker.BuildTest
dotnet run --project src/CicdPipeline.Worker.GitVersion
dotnet run --project src/CicdPipeline.Worker.Publish
dotnet run --project src/CicdPipeline.Worker.Deploy

# Dashboard (optional)
cd src/CicdPipeline.Dashboard && npm install && npm run dev
```

#### Option B: Docker Compose

Starts Temporal (with Postgres), the Temporal UI, registers search attributes, then launches all services:

```sh
docker-compose up --build
```

### Endpoints

| URL | Description |
|-----|-------------|
| `http://localhost:3000` | Pipeline Dashboard |
| `http://localhost:5100` | API |
| `http://localhost:8080` | Temporal UI |

### Trigger a Pipeline

```sh
curl -X POST http://localhost:5100/api/webhooks/github \
  -H 'Content-Type: application/json' \
  -d '{"repository":"test/repo","ref":"refs/heads/feature-1","commitSha":"abc1234567890","eventType":"push"}'
```

### API Endpoints

**Webhooks**

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/webhooks/github` | GitHub webhook receiver |
| POST | `/api/webhooks/generic` | Generic webhook receiver |

**Operator Controls**

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/ops/pipelines/{workflowId}/status` | Query workflow state |
| POST | `/api/ops/pipelines/{workflowId}/pause` | Pause a pipeline |
| POST | `/api/ops/pipelines/{workflowId}/cancel` | Cancel a pipeline |
| POST | `/api/ops/pipelines/{workflowId}/resume` | Resume a paused pipeline |
| POST | `/api/ops/deployments/start` | Start a deployment directly |
| GET | `/api/ops/pipelines` | List workflows (filterable by repository, status) |

**Health**

| Method | Path | Description |
|--------|------|-------------|
| GET | `/health` | Basic health check |
| GET | `/health/temporal` | Temporal connectivity check |
