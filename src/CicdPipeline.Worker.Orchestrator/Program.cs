using CicdPipeline.Contracts;
using CicdPipeline.ServiceDefaults;
using CicdPipeline.Workflows.Activities;
using CicdPipeline.Workflows.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Worker;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureTemporalWorker()
    .AddCicdWorkerTelemetry("CicdPipeline.Worker.Orchestrator", 9464)
    .Build();

var factory = host.Services.GetRequiredService<TemporalClientFactory>();
var client = await factory.CreateClientAsync();

var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var ingressActivities = new IngressActivities(loggerFactory.CreateLogger<IngressActivities>());
var metricsActivities = new MetricsActivities(loggerFactory.CreateLogger<MetricsActivities>());

using var worker = new TemporalWorker(
    client,
    new TemporalWorkerOptions(TaskQueues.Orchestrator)
        .AddWorkflow<PipelineIngressWorkflow>()
        .AddWorkflow<BuildValidationWorkflow>()
        .AddWorkflow<VersionAndPublishWorkflow>()
        .AddWorkflow<DeploymentWorkflow>()
        .AddAllActivities(ingressActivities)
        .AddAllActivities(metricsActivities));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await host.StartAsync(cts.Token);
try
{
    Console.WriteLine($"Orchestrator worker started on task queue: {TaskQueues.Orchestrator}");
    await worker.ExecuteAsync(cts.Token);
}
finally
{
    await host.StopAsync();
}
