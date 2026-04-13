using Temporalio.Client;

namespace CicdPipeline.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
           .WithTags("Health");

        app.MapGet("/health/temporal", async (TemporalClient client) =>
        {
            try
            {
                // Lightweight connectivity check
                await foreach (var _ in client.ListWorkflowsAsync(
                    "WorkflowId = 'health-check-probe'"))
                {
                    break;
                }

                return Results.Ok(new { temporal = "connected" });
            }
            catch (Exception)
            {
                return Results.StatusCode(503);
            }
        }).WithTags("Health");
    }
}
