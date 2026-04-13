using CicdPipeline.Contracts;
using CicdPipeline.ServiceDefaults;
using CicdPipeline.Workflows.Activities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Temporalio.Worker;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureTemporalWorker()
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

Console.WriteLine($"GitVersion worker started on task queue: {TaskQueues.GitVersion}");
await worker.ExecuteAsync(cts.Token);
