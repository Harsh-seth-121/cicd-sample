using CicdPipeline.Api.Endpoints;
using CicdPipeline.ServiceDefaults;
using Temporalio.Client;

var builder = WebApplication.CreateBuilder(args);

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

app.Run();
