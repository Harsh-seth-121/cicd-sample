using Temporalio.Client;
using SharedRegistration = CicdPipeline.ServiceDefaults.SearchAttributeRegistration;

namespace CicdPipeline.Workflows.Tests.Fixtures;

public static class SearchAttributeRegistration
{
    public static Task RegisterAllAsync(ITemporalClient client) =>
        SharedRegistration.EnsureRegisteredAsync(client);
}
