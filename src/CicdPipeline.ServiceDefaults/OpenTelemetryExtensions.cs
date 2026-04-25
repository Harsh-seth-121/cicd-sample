using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Temporalio.Extensions.OpenTelemetry;

namespace CicdPipeline.ServiceDefaults;

public static class OpenTelemetryExtensions
{
    private const string DefaultOtlpEndpoint = "http://otel-collector:4317";

    public static IServiceCollection AddCicdTelemetry(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null,
        Action<MeterProviderBuilder>? configureMetrics = null)
    {
        var otlpEndpoint = configuration["Otel:OtlpEndpoint"] ?? DefaultOtlpEndpoint;
        var resourceBuilder = BuildResource(serviceName);

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(resourceBuilder)
                    .AddSource(CicdPipelineMetrics.ActivitySourceName)
                    .AddSource(TracingInterceptor.ClientSource.Name)
                    .AddSource(TracingInterceptor.WorkflowsSource.Name)
                    .AddSource(TracingInterceptor.ActivitiesSource.Name)
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
                configureTracing?.Invoke(tracing);
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(resourceBuilder)
                    .AddMeter(CicdPipelineMetrics.MeterName)
                    .AddMeter("Temporalio")
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
                configureMetrics?.Invoke(metrics);
            });

        services.AddLogging(logging =>
        {
            logging.AddOpenTelemetry(otel =>
            {
                otel.SetResourceBuilder(resourceBuilder);
                otel.IncludeScopes = true;
                otel.IncludeFormattedMessage = true;
                otel.AddOtlpExporter(o => o.Endpoint = new Uri(otlpEndpoint));
            });
        });

        return services;
    }

    public static IHostBuilder AddCicdWorkerTelemetry(
        this IHostBuilder hostBuilder, string serviceName, int prometheusPort)
    {
        return hostBuilder.ConfigureServices((context, services) =>
        {
            services.AddCicdTelemetry(
                context.Configuration,
                serviceName,
                configureMetrics: metrics => metrics
                    .AddPrometheusHttpListener(o =>
                        o.UriPrefixes = [$"http://*:{prometheusPort}/"]));
        });
    }

    private static ResourceBuilder BuildResource(string serviceName) =>
        ResourceBuilder.CreateDefault()
            .AddService(serviceName, serviceVersion: "1.0.0");
}
