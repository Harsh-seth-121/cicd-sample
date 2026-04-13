using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CicdPipeline.ServiceDefaults;

public static class WorkerHostExtensions
{
    public static IHostBuilder ConfigureTemporalWorker(this IHostBuilder hostBuilder)
    {
        return hostBuilder.ConfigureServices((context, services) =>
        {
            services.Configure<TemporalSettings>(
                context.Configuration.GetSection("Temporal"));
            services.AddSingleton<TemporalClientFactory>();
        });
    }
}
