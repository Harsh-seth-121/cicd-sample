using Microsoft.Extensions.Logging;
using Temporalio.Api.Enums.V1;
using Temporalio.Api.OperatorService.V1;
using Temporalio.Client;

namespace CicdPipeline.ServiceDefaults;

public static class SearchAttributeRegistration
{
    private static readonly Dictionary<string, IndexedValueType> Attributes = new()
    {
        ["CicdPipelineStatus"] = IndexedValueType.Keyword,
        ["CicdBranch"] = IndexedValueType.Keyword,
        ["CicdRepository"] = IndexedValueType.Keyword,
        ["CicdCommitSha"] = IndexedValueType.Keyword,
        ["CicdStage"] = IndexedValueType.Keyword,
        ["CicdPipelineStartedAt"] = IndexedValueType.Datetime,
        ["CicdSemVer"] = IndexedValueType.Keyword,
        ["CicdImageDigest"] = IndexedValueType.Keyword,
        ["CicdTriggerType"] = IndexedValueType.Keyword,
    };

    public static async Task EnsureRegisteredAsync(ITemporalClient client, ILogger? logger = null)
    {
        var existing = await client.Connection.OperatorService.ListSearchAttributesAsync(
            new ListSearchAttributesRequest { Namespace = client.Options.Namespace });

        var toAdd = new AddSearchAttributesRequest
        {
            Namespace = client.Options.Namespace,
        };

        foreach (var (name, type) in Attributes)
        {
            if (!existing.CustomAttributes.ContainsKey(name))
            {
                toAdd.SearchAttributes.Add(name, type);
            }
        }

        if (toAdd.SearchAttributes.Count == 0)
        {
            logger?.LogInformation("All custom search attributes already registered");
            return;
        }

        await client.Connection.OperatorService.AddSearchAttributesAsync(toAdd);
        logger?.LogInformation("Registered {Count} custom search attributes", toAdd.SearchAttributes.Count);
    }
}
