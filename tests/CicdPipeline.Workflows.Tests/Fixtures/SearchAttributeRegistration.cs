using Temporalio.Api.Enums.V1;
using Temporalio.Api.OperatorService.V1;
using Temporalio.Client;

namespace CicdPipeline.Workflows.Tests.Fixtures;

public static class SearchAttributeRegistration
{
    public static async Task RegisterAllAsync(ITemporalClient client)
    {
        var request = new AddSearchAttributesRequest
        {
            Namespace = client.Options.Namespace,
        };

        request.SearchAttributes.Add("CicdPipelineStatus", IndexedValueType.Keyword);
        request.SearchAttributes.Add("CicdBranch", IndexedValueType.Keyword);
        request.SearchAttributes.Add("CicdRepository", IndexedValueType.Keyword);
        request.SearchAttributes.Add("CicdCommitSha", IndexedValueType.Keyword);
        request.SearchAttributes.Add("CicdStage", IndexedValueType.Keyword);
        request.SearchAttributes.Add("CicdPipelineStartedAt", IndexedValueType.Datetime);
        request.SearchAttributes.Add("CicdSemVer", IndexedValueType.Keyword);
        request.SearchAttributes.Add("CicdImageDigest", IndexedValueType.Keyword);
        request.SearchAttributes.Add("CicdTriggerType", IndexedValueType.Keyword);

        await client.Connection.OperatorService.AddSearchAttributesAsync(request);
    }
}
