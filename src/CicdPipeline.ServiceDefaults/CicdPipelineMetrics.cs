using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CicdPipeline.ServiceDefaults;

public static class CicdPipelineMetrics
{
    public const string ServiceName = "CicdPipeline";
    public const string MeterName = "CicdPipeline";
    public const string ActivitySourceName = "CicdPipeline";

    public static readonly Meter Meter = new(MeterName, "1.0.0");
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName, "1.0.0");

    public static readonly Counter<long> PipelineStarted =
        Meter.CreateCounter<long>("cicd.pipeline.started", "pipelines", "Number of pipelines started");

    public static readonly Counter<long> PipelineCompleted =
        Meter.CreateCounter<long>("cicd.pipeline.completed", "pipelines", "Number of pipelines completed");

    public static readonly Counter<long> ActivityExecuted =
        Meter.CreateCounter<long>("cicd.activity.executed", "activities", "Number of activity executions");

    public static readonly Histogram<double> StageDuration =
        Meter.CreateHistogram<double>("cicd.stage.duration", "s", "Duration of pipeline stages in seconds");

    public static readonly Histogram<double> PipelineDuration =
        Meter.CreateHistogram<double>("cicd.pipeline.duration", "s", "Total pipeline duration in seconds");
}
