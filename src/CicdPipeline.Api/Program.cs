using CicdPipeline.Api.Endpoints;
using CicdPipeline.ServiceDefaults;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using Temporalio.Client;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCicdTelemetry(
    builder.Configuration,
    "CicdPipeline.Api",
    configureTracing: tracing => tracing.AddAspNetCoreInstrumentation(),
    configureMetrics: metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddPrometheusExporter());

builder.Services.Configure<TemporalSettings>(
    builder.Configuration.GetSection("Temporal"));
builder.Services.AddSingleton<TemporalClientFactory>();
builder.Services.AddSingleton<TemporalClient>(sp =>
    sp.GetRequiredService<TemporalClientFactory>().CreateClientAsync().GetAwaiter().GetResult());

builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

app.MapWebhookEndpoints();
app.MapOpsEndpoints();
app.MapHealthEndpoints();
app.MapPrometheusScrapingEndpoint();

app.Run();
