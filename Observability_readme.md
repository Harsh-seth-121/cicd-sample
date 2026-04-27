# Plan: Add OpenTelemetry, Prometheus, and Grafana Telemetry

## Context

The project currently has **zero observability** beyond `ILogger<T>` console output. There are no traces, no metrics, no structured log export. This plan adds full telemetry (tracing, metrics, logging) using OpenTelemetry, with Prometheus for metrics storage, Grafana for dashboards, and an OTel Collector for traces/logs — all containerized in docker-compose.

## Architecture Overview

```
.NET Services (API + 5 Workers)
  ├── Traces + Logs  ──► OTLP gRPC ──► OTel Collector (port 4317) ──► debug/stdout
  └── Metrics         ──► /metrics  ──► Prometheus (port 9090) ──► Grafana (port 3000)
```

- **API** exposes `/metrics` via ASP.NET Core Prometheus exporter
- **Workers** expose `/metrics` via standalone Prometheus HttpListener (ports 9464-9468)
- **All services** send traces and logs via OTLP to the OTel Collector
- **Temporal SDK** instrumented with `TracingInterceptor` (distributed traces across workflow/activity boundaries) and `CustomMetricMeter` (Temporal Core SDK internal metrics)

---

## Step 1: Add NuGet packages to `Directory.Packages.props`

**File:** `Directory.Packages.props`

Add these entries to the existing `<ItemGroup>`:

```xml
<!-- OpenTelemetry Core -->
<PackageVersion Include="OpenTelemetry" Version="1.15.3" />
<PackageVersion Include="OpenTelemetry.Api" Version="1.15.3" />
<PackageVersion Include="OpenTelemetry.Extensions.Hosting" Version="1.15.3" />

<!-- OTel Instrumentation -->
<PackageVersion Include="OpenTelemetry.Instrumentation.AspNetCore" Version="1.15.2" />
<PackageVersion Include="OpenTelemetry.Instrumentation.Http" Version="1.15.1" />
<PackageVersion Include="OpenTelemetry.Instrumentation.Runtime" Version="1.15.1" />

<!-- OTel Exporters -->
<PackageVersion Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.15.3" />
<PackageVersion Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" Version="1.15.3-beta.1" />
<PackageVersion Include="OpenTelemetry.Exporter.Prometheus.HttpListener" Version="1.15.3-beta.1" />

<!-- Temporal OTel Extensions -->
<PackageVersion Include="Temporalio.Extensions.OpenTelemetry" Version="1.13.0" />
<PackageVersion Include="Temporalio.Extensions.DiagnosticSource" Version="1.13.0" />
```

**Versions verified on NuGet** (2026-04-25). The Prometheus exporters are still pre-release (`beta.1`).

---

## Step 2: Handle `TreatWarningsAsErrors` for pre-release packages

**File:** `Directory.Build.props`

Add `<NoWarn>` for pre-release NuGet warning since the Prometheus exporters are beta:

```xml
<NoWarn>$(NoWarn);NU5104</NoWarn>
```

---

## Step 3: Add PackageReferences to `.csproj` files

### `src/CicdPipeline.ServiceDefaults/CicdPipeline.ServiceDefaults.csproj`

Add to the existing `<ItemGroup>`:

```xml
<!-- OpenTelemetry -->
<PackageReference Include="OpenTelemetry" />
<PackageReference Include="OpenTelemetry.Api" />
<PackageReference Include="OpenTelemetry.Extensions.Hosting" />
<PackageReference Include="OpenTelemetry.Instrumentation.Http" />
<PackageReference Include="OpenTelemetry.Instrumentation.Runtime" />
<PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.HttpListener" />

<!-- Temporal OTel Extensions -->
<PackageReference Include="Temporalio.Extensions.OpenTelemetry" />
<PackageReference Include="Temporalio.Extensions.DiagnosticSource" />
```

All 5 worker projects already reference ServiceDefaults transitively, so they need no changes.

### `src/CicdPipeline.Api/CicdPipeline.Api.csproj`

Add to existing `<ItemGroup>`:

```xml
<PackageReference Include="OpenTelemetry.Instrumentation.AspNetCore" />
<PackageReference Include="OpenTelemetry.Exporter.Prometheus.AspNetCore" />
```

---

## Step 4: Create `CicdPipelineMetrics.cs` in ServiceDefaults

**New file:** `src/CicdPipeline.ServiceDefaults/CicdPipelineMetrics.cs`

Define a static class with:
- A `System.Diagnostics.Metrics.Meter` named `"CicdPipeline"`
- A `System.Diagnostics.ActivitySource` named `"CicdPipeline"`
- Custom instruments:
  - `cicd.pipeline.started` (Counter) — pipelines started, tagged by repo/trigger_type/branch
  - `cicd.pipeline.completed` (Counter) — pipelines completed, tagged by repo/status
  - `cicd.activity.executed` (Counter) — activity executions, tagged by activity name/task queue
  - `cicd.stage.duration` (Histogram, seconds) — stage durations, tagged by stage/repo
  - `cicd.pipeline.duration` (Histogram, seconds) — total pipeline duration, tagged by repo/status

---

## Step 5: Create `OpenTelemetryExtensions.cs` in ServiceDefaults

**New file:** `src/CicdPipeline.ServiceDefaults/OpenTelemetryExtensions.cs`

Two extension methods:

### `AddCicdApiTelemetry(this WebApplicationBuilder, string serviceName)`
For the API project:
- Configures tracing: custom ActivitySource + Temporal TracingInterceptor sources + ASP.NET Core + HttpClient instrumentation → OTLP exporter
- Configures metrics: custom Meter + `"Temporalio"` meter + ASP.NET Core + HttpClient + Runtime instrumentation → Prometheus ASP.NET Core exporter
- Configures logging: OTLP exporter with scopes + formatted messages
- OTLP endpoint read from `Otel:OtlpEndpoint` config (default: `http://otel-collector:4317`)

### `AddCicdWorkerTelemetry(this IHostBuilder, string serviceName, int prometheusPort)`
For worker projects:
- Same tracing/logging as API (minus ASP.NET Core instrumentation)
- Metrics exported via `PrometheusHttpListener` on the specified port
- Each worker gets a unique port (9464-9468)

---

## Step 6: Modify `TemporalClientFactory.cs`

**File:** `src/CicdPipeline.ServiceDefaults/TemporalClientFactory.cs`

Wire Temporal OTel interceptors into the client connect options:
- Add `TracingInterceptor` from `Temporalio.Extensions.OpenTelemetry` to `Interceptors` — propagates trace context across workflow/activity boundaries via Temporal headers
- Create a `TemporalRuntime` with `CustomMetricMeter` from `Temporalio.Extensions.DiagnosticSource` — bridges Temporal Core SDK internal metrics (`temporal_request`, `temporal_request_latency`, etc.) into the .NET Meter pipeline
- Both should be created once and stored as instance fields (the factory is already a singleton)

---

## Step 7: Update Program.cs files (1 API + 5 Workers)

### `src/CicdPipeline.Api/Program.cs`
Add two lines:
1. `builder.AddCicdApiTelemetry("CicdPipeline.Api");` — before `builder.Build()`
2. `app.MapPrometheusScrapingEndpoint();` — before `app.Run()`

### Worker Program.cs files
Add one line to each — chain `.AddCicdWorkerTelemetry(serviceName, port)` after `.ConfigureTemporalWorker()`:

| Worker | Service Name | Prometheus Port |
|---|---|---|
| `src/CicdPipeline.Worker.Orchestrator/Program.cs` | `CicdPipeline.Worker.Orchestrator` | 9464 |
| `src/CicdPipeline.Worker.BuildTest/Program.cs` | `CicdPipeline.Worker.BuildTest` | 9465 |
| `src/CicdPipeline.Worker.GitVersion/Program.cs` | `CicdPipeline.Worker.GitVersion` | 9466 |
| `src/CicdPipeline.Worker.Publish/Program.cs` | `CicdPipeline.Worker.Publish` | 9467 |
| `src/CicdPipeline.Worker.Deploy/Program.cs` | `CicdPipeline.Worker.Deploy` | 9468 |

---

## Step 8: Add custom metric recording to activities and API

### Webhook endpoints (`src/CicdPipeline.Api/Endpoints/WebhookEndpoints.cs`)
After successful `StartWorkflowAsync`, call `CicdPipelineMetrics.PipelineStarted.Add(1, tags)`.

### Activity classes (`src/CicdPipeline.Workflows/Activities/*.cs`)
Add `Stopwatch`-based duration recording and `ActivityExecuted` counter increments at the end of each activity method. Example pattern:

```csharp
CicdPipelineMetrics.ActivityExecuted.Add(1, new TagList { { "activity", "Build" }, { "task_queue", "cicd.build.test" } });
CicdPipelineMetrics.StageDuration.Record(elapsed.TotalSeconds, new TagList { { "stage", "Build" } });
```

### Pipeline completion metrics
Create a lightweight `MetricsActivities.cs` in `src/CicdPipeline.Workflows/Activities/` with a `RecordPipelineCompletedAsync` activity. Call it at the end of `PipelineIngressWorkflow.RunAsync`. This is deterministic-safe because it runs as a Temporal activity, not inline in the workflow.

**Important:** Do NOT record metrics directly inside workflow code — that violates Temporal determinism. All custom metric recording must happen in activities or the API layer.

---

## Step 9: Create infrastructure config files

### `otel-collector-config.yaml` (new file, project root)
- Receivers: OTLP gRPC (0.0.0.0:4317) + OTLP HTTP (0.0.0.0:4318)
- Processors: batch (5s timeout, 1024 batch size)
- Exporters: `debug` (stdout with detailed verbosity) — extendable to Jaeger/Tempo later
- Pipelines: traces → [otlp] → [batch] → [debug], logs → [otlp] → [batch] → [debug]

### `prometheus.yml` (new file, project root)
- Global scrape interval: 15s
- 6 scrape jobs:
  - `cicd-api` → `api:8080/metrics`
  - `cicd-worker-orchestrator` → `worker-orchestrator:9464/metrics`
  - `cicd-worker-buildtest` → `worker-buildtest:9465/metrics`
  - `cicd-worker-gitversion` → `worker-gitversion:9466/metrics`
  - `cicd-worker-publish` → `worker-publish:9467/metrics`
  - `cicd-worker-deploy` → `worker-deploy:9468/metrics`

### `grafana/provisioning/datasources/datasource.yml` (new file)
- Prometheus datasource at `http://prometheus:9090`, set as default

### `grafana/provisioning/dashboards/dashboards.yml` (new file)
- File-based dashboard provider pointing to `/etc/grafana/provisioning/dashboards`

### `grafana/provisioning/dashboards/cicd-pipeline.json` (new file)
Starter dashboard with panels:
- Pipeline throughput (`rate(cicd_pipeline_started_total)`)
- Pipeline completion by status (`cicd_pipeline_completed_total` by `status`)
- Stage duration heatmap (`cicd_stage_duration_seconds`)
- Pipeline duration P50/P95/P99
- Activity execution rate by name
- Temporal request rate and latency
- .NET runtime metrics (GC, thread pool, process CPU)

---

## Step 10: Update `docker-compose.yml`

### Add 3 new services

```yaml
otel-collector:
  image: otel/opentelemetry-collector-contrib:0.115.0
  command: ["--config=/etc/otelcol/config.yaml"]
  volumes:
    - ./otel-collector-config.yaml:/etc/otelcol/config.yaml:ro
  ports:
    - "4317:4317"
    - "4318:4318"
  networks:
    - cicd

prometheus:
  image: prom/prometheus:v3.2.1
  volumes:
    - ./prometheus.yml:/etc/prometheus/prometheus.yml:ro
  ports:
    - "9090:9090"
  depends_on:
    - api
  networks:
    - cicd

grafana:
  image: grafana/grafana:11.5.2
  environment:
    - GF_SECURITY_ADMIN_PASSWORD=admin
    - GF_AUTH_ANONYMOUS_ENABLED=true
    - GF_AUTH_ANONYMOUS_ORG_ROLE=Viewer
  volumes:
    - ./grafana/provisioning:/etc/grafana/provisioning:ro
  ports:
    - "3030:3000"
  depends_on:
    - prometheus
  networks:
    - cicd
```

### Update all 6 .NET services
Add environment variable to each:
```yaml
- Otel__OtlpEndpoint=http://otel-collector:4317
```

Add soft dependency on otel-collector:
```yaml
depends_on:
  otel-collector:
    condition: service_started
```

---

## Files Modified (existing)

| File | Change |
|---|---|
| `Directory.Packages.props` | Add 11 OTel + Temporal extension package versions |
| `Directory.Build.props` | Add `NU5104` to `NoWarn` for pre-release packages |
| `src/CicdPipeline.ServiceDefaults/CicdPipeline.ServiceDefaults.csproj` | Add 9 PackageReferences |
| `src/CicdPipeline.ServiceDefaults/TemporalClientFactory.cs` | Wire TracingInterceptor + CustomMetricMeter |
| `src/CicdPipeline.Api/CicdPipeline.Api.csproj` | Add 2 PackageReferences |
| `src/CicdPipeline.Api/Program.cs` | Add 2 lines (telemetry + Prometheus endpoint) |
| `src/CicdPipeline.Api/Endpoints/WebhookEndpoints.cs` | Add pipeline started counter |
| `src/CicdPipeline.Worker.Orchestrator/Program.cs` | Add 1 line |
| `src/CicdPipeline.Worker.BuildTest/Program.cs` | Add 1 line |
| `src/CicdPipeline.Worker.GitVersion/Program.cs` | Add 1 line |
| `src/CicdPipeline.Worker.Publish/Program.cs` | Add 1 line |
| `src/CicdPipeline.Worker.Deploy/Program.cs` | Add 1 line |
| `src/CicdPipeline.Workflows/Activities/BuildTestActivities.cs` | Add metric recording |
| `src/CicdPipeline.Workflows/Activities/GitVersionActivities.cs` | Add metric recording |
| `src/CicdPipeline.Workflows/Activities/PublishActivities.cs` | Add metric recording |
| `src/CicdPipeline.Workflows/Activities/DeployActivities.cs` | Add metric recording |
| `src/CicdPipeline.Workflows/Activities/IngressActivities.cs` | Add metric recording |
| `src/CicdPipeline.Workflows/Workflows/PipelineIngressWorkflow.cs` | Call MetricsActivities at end |
| `docker-compose.yml` | Add 3 services + env vars on 6 existing services |

## Files Created (new)

| File | Purpose |
|---|---|
| `src/CicdPipeline.ServiceDefaults/CicdPipelineMetrics.cs` | Custom meter, activity source, counters, histograms |
| `src/CicdPipeline.ServiceDefaults/OpenTelemetryExtensions.cs` | Centralized OTel setup for API + workers |
| `src/CicdPipeline.Workflows/Activities/MetricsActivities.cs` | Pipeline completion recording activity |
| `otel-collector-config.yaml` | OTel Collector receiver/processor/exporter config |
| `prometheus.yml` | Prometheus scrape config for all 6 services |
| `grafana/provisioning/datasources/datasource.yml` | Grafana Prometheus datasource |
| `grafana/provisioning/dashboards/dashboards.yml` | Grafana dashboard provider |
| `grafana/provisioning/dashboards/cicd-pipeline.json` | Starter Grafana dashboard |

---

## Implementation Order

1. `Directory.Packages.props` + `Directory.Build.props` (package versions + warning suppression)
2. `.csproj` files (ServiceDefaults + Api)
3. `CicdPipelineMetrics.cs` (no dependencies)
4. `OpenTelemetryExtensions.cs` (depends on metrics class)
5. `TemporalClientFactory.cs` (wire interceptors)
6. `Program.cs` files (API + 5 workers)
7. `MetricsActivities.cs` + activity instrumentation + workflow change
8. `WebhookEndpoints.cs` (API metric recording)
9. Config files: `otel-collector-config.yaml`, `prometheus.yml`, `grafana/provisioning/`
10. `docker-compose.yml` (add services + env vars)

---

## Verification

1. `dotnet build` — must pass with zero warnings
2. `dotnet test` — existing tests must pass
3. `docker-compose up --build` — all containers start healthy
4. Trigger a pipeline via curl, then verify:
   - **Prometheus** (`http://localhost:9090`): query `cicd_pipeline_started_total`, `temporal_request_total`, `process_runtime_dotnet_gc_collections_count_total`
   - **Grafana** (`http://localhost:3030`): dashboard shows data
   - **OTel Collector logs** (`docker-compose logs otel-collector`): trace spans visible in stdout

---

## Ports Summary (after changes)

| Service | Port | Purpose |
|---|---|---|
| Temporal gRPC | 7233 | Temporal server |
| Temporal UI | 8080 | Temporal web UI |
| API | 5100 | Application API |
| Dashboard | 3001 | React frontend |
| OTel Collector gRPC | 4317 | OTLP trace/log ingestion |
| OTel Collector HTTP | 4318 | OTLP trace/log ingestion |
| Prometheus | 9090 | Metrics storage + UI |
| Grafana | 3030 | Dashboards |
