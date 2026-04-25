using System.Diagnostics;
using CicdPipeline.ServiceDefaults;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace CicdPipeline.Workflows.Activities;

public class MetricsActivities
{
    private readonly ILogger<MetricsActivities> _logger;

    public MetricsActivities(ILogger<MetricsActivities> logger)
    {
        _logger = logger;
    }

    [Activity]
    public Task RecordPipelineCompletedAsync(string repository, string status, double durationSeconds)
    {
        CicdPipelineMetrics.PipelineCompleted.Add(1, new TagList
        {
            { "repository", repository },
            { "status", status },
        });
        CicdPipelineMetrics.PipelineDuration.Record(durationSeconds, new TagList
        {
            { "repository", repository },
            { "status", status },
        });

        _logger.LogInformation(
            "Pipeline completed for {Repository}: status={Status} duration={Duration}s",
            repository, status, durationSeconds);

        return Task.CompletedTask;
    }
}
