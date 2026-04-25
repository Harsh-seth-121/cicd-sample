FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG PROJECT_PATH
WORKDIR /src

# Copy central build/package files first for layer caching
COPY Directory.Build.props Directory.Packages.props CicdPipeline.sln ./

# Copy all project files for restore
COPY src/CicdPipeline.Contracts/CicdPipeline.Contracts.csproj src/CicdPipeline.Contracts/
COPY src/CicdPipeline.ServiceDefaults/CicdPipeline.ServiceDefaults.csproj src/CicdPipeline.ServiceDefaults/
COPY src/CicdPipeline.Workflows/CicdPipeline.Workflows.csproj src/CicdPipeline.Workflows/
COPY src/CicdPipeline.Api/CicdPipeline.Api.csproj src/CicdPipeline.Api/
COPY src/CicdPipeline.Worker.Orchestrator/CicdPipeline.Worker.Orchestrator.csproj src/CicdPipeline.Worker.Orchestrator/
COPY src/CicdPipeline.Worker.BuildTest/CicdPipeline.Worker.BuildTest.csproj src/CicdPipeline.Worker.BuildTest/
COPY src/CicdPipeline.Worker.GitVersion/CicdPipeline.Worker.GitVersion.csproj src/CicdPipeline.Worker.GitVersion/
COPY src/CicdPipeline.Worker.Publish/CicdPipeline.Worker.Publish.csproj src/CicdPipeline.Worker.Publish/
COPY src/CicdPipeline.Worker.Deploy/CicdPipeline.Worker.Deploy.csproj src/CicdPipeline.Worker.Deploy/

RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet restore ${PROJECT_PATH}

# Copy all source and build
COPY src/ src/
RUN --mount=type=cache,id=nuget,target=/root/.nuget/packages,sharing=locked \
    dotnet publish ${PROJECT_PATH} -c Release -o /app/publish

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet"]
# CMD is set per-service in docker-compose
