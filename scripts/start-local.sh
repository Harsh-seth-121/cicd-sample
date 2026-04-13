#!/usr/bin/env bash
set -euo pipefail

NAMESPACE="cicd-prodctl"
ADDRESS="localhost:7233"
DB_FILE="/tmp/temporal-cicd.db"

echo "Starting Temporal dev server (namespace: $NAMESPACE)..."
temporal server start-dev \
  --namespace "$NAMESPACE" \
  --db-filename "$DB_FILE" \
  --ui-port 8080 &
TEMPORAL_PID=$!

cleanup() {
  echo ""
  echo "Stopping Temporal (PID $TEMPORAL_PID)..."
  kill "$TEMPORAL_PID" 2>/dev/null || true
  wait "$TEMPORAL_PID" 2>/dev/null || true
}
trap cleanup EXIT INT TERM

echo "Waiting for Temporal to be ready..."
for i in $(seq 1 30); do
  if temporal operator namespace describe --namespace "$NAMESPACE" --address "$ADDRESS" >/dev/null 2>&1; then
    echo "Temporal is ready."
    break
  fi
  if [ "$i" -eq 30 ]; then
    echo "ERROR: Temporal did not become ready in 30 seconds."
    exit 1
  fi
  sleep 1
done

echo "Registering search attributes..."
temporal operator search-attribute create --namespace "$NAMESPACE" --address "$ADDRESS" --name CicdPipelineStatus --type Keyword 2>/dev/null || true
temporal operator search-attribute create --namespace "$NAMESPACE" --address "$ADDRESS" --name CicdBranch --type Keyword 2>/dev/null || true
temporal operator search-attribute create --namespace "$NAMESPACE" --address "$ADDRESS" --name CicdRepository --type Keyword 2>/dev/null || true
temporal operator search-attribute create --namespace "$NAMESPACE" --address "$ADDRESS" --name CicdCommitSha --type Keyword 2>/dev/null || true
temporal operator search-attribute create --namespace "$NAMESPACE" --address "$ADDRESS" --name CicdStage --type Keyword 2>/dev/null || true
temporal operator search-attribute create --namespace "$NAMESPACE" --address "$ADDRESS" --name CicdPipelineStartedAt --type DateTime 2>/dev/null || true
temporal operator search-attribute create --namespace "$NAMESPACE" --address "$ADDRESS" --name CicdSemVer --type Keyword 2>/dev/null || true
temporal operator search-attribute create --namespace "$NAMESPACE" --address "$ADDRESS" --name CicdImageDigest --type Keyword 2>/dev/null || true
temporal operator search-attribute create --namespace "$NAMESPACE" --address "$ADDRESS" --name CicdTriggerType --type Keyword 2>/dev/null || true
echo "Search attributes registered."

echo ""
echo "========================================="
echo " Temporal is running"
echo " UI:      http://localhost:8080"
echo " gRPC:    localhost:7233"
echo " DB:      $DB_FILE"
echo "========================================="
echo ""
echo "Start services in separate terminals:"
echo "  dotnet run --project src/CicdPipeline.Api"
echo "  dotnet run --project src/CicdPipeline.Worker.Orchestrator"
echo "  dotnet run --project src/CicdPipeline.Worker.BuildTest"
echo "  dotnet run --project src/CicdPipeline.Worker.GitVersion"
echo "  dotnet run --project src/CicdPipeline.Worker.Publish"
echo "  dotnet run --project src/CicdPipeline.Worker.Deploy"
echo ""
echo "Press Ctrl+C to stop Temporal."
wait "$TEMPORAL_PID"
