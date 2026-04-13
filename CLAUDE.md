@.claude/rules/temporal_cicd_mermai_pack/


# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Temporal Cloud CI/CD pipeline orchestrator built with .NET 8 and the Temporal .NET SDK. Includes 4 workflows, 5 activity classes, an ASP.NET Core API, and 5 dedicated workers — each on its own task queue.

## Repository State

- Branch `init` contains the initial scaffold (README, .gitignore, LICENSE)
- Branch `main` is the target integration branch
- Remote: https://github.com/Harsh-seth-121/cicd-sample.git

## Tech Stack

- **.NET 8 / C#** — MSBuild, NuGet packages (central package management)
- **Temporal** — Temporalio .NET SDK 1.13.0
- **GitHub Actions** for CI/CD

## Build Commands

```sh
dotnet build
dotnet test
dotnet format
```

## Running Locally

### Option A: Local Temporal dev server

Requires: `temporal` CLI, .NET 8 SDK

```sh
# Start Temporal + register namespace and search attributes
./scripts/start-local.sh

# In separate terminals:
dotnet run --project src/CicdPipeline.Api
dotnet run --project src/CicdPipeline.Worker.Orchestrator
dotnet run --project src/CicdPipeline.Worker.BuildTest
dotnet run --project src/CicdPipeline.Worker.GitVersion
dotnet run --project src/CicdPipeline.Worker.Publish
dotnet run --project src/CicdPipeline.Worker.Deploy
```

### Option B: Docker Compose

```sh
docker-compose up --build
```

This starts Temporal, the UI, registers search attributes, then launches all services.

- API: http://localhost:5100
- Temporal UI: http://localhost:8080

### Trigger a pipeline

```sh
curl -X POST http://localhost:5100/api/webhooks/github \
  -H 'Content-Type: application/json' \
  -d '{"repository":"test/repo","ref":"refs/heads/feature-1","commitSha":"abc1234567890","eventType":"push"}'
```
