using System.Diagnostics;
using CicdPipeline.Contracts.Models;
using CicdPipeline.ServiceDefaults;
using Microsoft.Extensions.Logging;
using Temporalio.Activities;

namespace CicdPipeline.Workflows.Activities;

public class PublishActivities
{
    private readonly ILogger<PublishActivities> _logger;

    public PublishActivities(ILogger<PublishActivities> logger)
    {
        _logger = logger;
    }

    [Activity]
    public async Task<ImageMetadata> PrepareImageMetadataAsync(
        NormalizedPipelineMetadata metadata, VersionInfo version)
    {
        _logger.LogInformation(
            "Preparing image metadata for {Repository} v{Version}",
            metadata.Repository, version.SemVer);

        await Task.CompletedTask;

        var registry = "registry.example.com";
        var imageName = $"{metadata.Repository}";
        var tag = version.SemVer;

        return new ImageMetadata(
            ImageName: imageName,
            Tag: tag,
            Digest: null, // Will be captured after push
            Registry: registry,
            FullImageRef: $"{registry}/{imageName}:{tag}");
    }

    [Activity]
    public async Task BuildOrFinalizeImageAsync(ImageMetadata image, string commitSha)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation(
            "Building image {ImageName}:{Tag} for commit {Sha}",
            image.ImageName, image.Tag, commitSha);

        // TODO: Shell out to container build tool (docker build / buildah / kaniko)
        for (var i = 0; i < 3; i++)
        {
            ActivityExecutionContext.Current.Heartbeat();
            await Task.Delay(100);
        }

        RecordMetrics("BuildOrFinalizeImage", sw.Elapsed);
    }

    [Activity]
    public async Task PushImageAsync(ImageMetadata image)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Pushing image {FullImageRef}", image.FullImageRef);

        // TODO: Push to container registry
        for (var i = 0; i < 2; i++)
        {
            ActivityExecutionContext.Current.Heartbeat();
            await Task.Delay(100);
        }

        RecordMetrics("PushImage", sw.Elapsed);
    }

    [Activity]
    public async Task<ImageMetadata> CaptureDigestAsync(ImageMetadata image)
    {
        _logger.LogInformation("Capturing digest for {ImageName}:{Tag}", image.ImageName, image.Tag);

        // TODO: Query registry for the immutable digest
        await Task.Delay(50);

        var digest = $"sha256:{Guid.NewGuid():N}";
        return image with
        {
            Digest = digest,
            FullImageRef = $"{image.Registry}/{image.ImageName}@{digest}",
        };
    }

    [Activity]
    public async Task<ReleaseManifest> WriteReleaseManifestAsync(
        NormalizedPipelineMetadata metadata, VersionInfo version, ImageMetadata image)
    {
        _logger.LogInformation(
            "Writing release manifest for pipeline {PipelineId}", metadata.PipelineId);

        // TODO: Write release manifest to manifest store
        await Task.Delay(50);

        return new ReleaseManifest(
            PipelineId: metadata.PipelineId,
            Repository: metadata.Repository,
            CommitSha: metadata.CommitSha,
            Version: version,
            Image: image,
            CreatedAt: DateTimeOffset.UtcNow,
            Labels: new Dictionary<string, string>
            {
                ["branch"] = metadata.Branch,
                ["sha"] = metadata.CommitSha,
                ["semver"] = version.SemVer,
            });
    }

    private static void RecordMetrics(string activityName, TimeSpan elapsed)
    {
        CicdPipelineMetrics.ActivityExecuted.Add(1, new TagList
        {
            { "activity", activityName },
            { "task_queue", "cicd.publish" },
        });
        CicdPipelineMetrics.StageDuration.Record(elapsed.TotalSeconds, new TagList
        {
            { "stage", activityName },
        });
    }
}
