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

    public static async Task TrackActivity(
        string activity,
        string taskQueue,
        Func<Task> body,
        params (string Key, object? Value)[] extraTags)
    {
        var sw = Stopwatch.StartNew();
        var status = "success";
        try
        {
            await body();
        }
        catch (OperationCanceledException)
        {
            status = "canceled";
            throw;
        }
        catch
        {
            status = "failed";
            throw;
        }
        finally
        {
            RecordExecution(activity, taskQueue, status, sw.Elapsed, extraTags);
        }
    }

    public static async Task<T> TrackActivity<T>(
        string activity,
        string taskQueue,
        Func<Task<T>> body,
        params (string Key, object? Value)[] extraTags)
    {
        var sw = Stopwatch.StartNew();
        var status = "success";
        try
        {
            return await body();
        }
        catch (OperationCanceledException)
        {
            status = "canceled";
            throw;
        }
        catch
        {
            status = "failed";
            throw;
        }
        finally
        {
            RecordExecution(activity, taskQueue, status, sw.Elapsed, extraTags);
        }
    }

    private static void RecordExecution(
        string activity,
        string taskQueue,
        string status,
        TimeSpan elapsed,
        (string Key, object? Value)[] extraTags)
    {
        var counterTags = new TagList
        {
            { "activity", activity },
            { "task_queue", taskQueue },
            { "status", status },
        };
        var stageTags = new TagList
        {
            { "stage", activity },
            { "status", status },
        };
        foreach (var (key, value) in extraTags)
        {
            counterTags.Add(key, value);
            stageTags.Add(key, value);
        }
        ActivityExecuted.Add(1, counterTags);
        StageDuration.Record(elapsed.TotalSeconds, stageTags);
    }
}
