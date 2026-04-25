using CicdPipeline.Contracts;
using CicdPipeline.ServiceDefaults;
using CicdPipeline.Workflows.Activities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Worker;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureTemporalWorker()
    .AddCicdWorkerTelemetry("CicdPipeline.Worker.GitVersion", 9466)
    .Build();

var factory = host.Services.GetRequiredService<TemporalClientFactory>();
var client = await factory.CreateClientAsync();

var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();
var activities = new GitVersionActivities(loggerFactory.CreateLogger<GitVersionActivities>());

using var worker = new TemporalWorker(
    client,
    new TemporalWorkerOptions(TaskQueues.GitVersion)
        .AddAllActivities(activities));

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

await host.StartAsync(cts.Token);
try
{
    Console.WriteLine($"GitVersion worker started on task queue: {TaskQueues.GitVersion}");
    await worker.ExecuteAsync(cts.Token);
}
finally
{
    await host.StopAsync();
}
