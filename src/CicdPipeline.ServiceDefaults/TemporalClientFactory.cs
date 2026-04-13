using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Temporalio.Client;

namespace CicdPipeline.ServiceDefaults;

public class TemporalClientFactory
{
    private readonly TemporalSettings _settings;
    private readonly ILogger<TemporalClientFactory> _logger;

    private const int MaxRetries = 6;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);

    public TemporalClientFactory(IOptions<TemporalSettings> settings, ILogger<TemporalClientFactory> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<TemporalClient> CreateClientAsync()
    {
        var options = new TemporalClientConnectOptions(_settings.ServerAddress)
        {
            Namespace = _settings.Namespace,
        };

        if (HasTlsConfiguration())
        {
            byte[] clientCert = _settings.TlsCertPem is not null
                ? Encoding.UTF8.GetBytes(_settings.TlsCertPem)
                : await File.ReadAllBytesAsync(_settings.TlsCertPath);

            byte[] clientKey = _settings.TlsKeyPem is not null
                ? Encoding.UTF8.GetBytes(_settings.TlsKeyPem)
                : await File.ReadAllBytesAsync(_settings.TlsKeyPath);

            options.Tls = new TlsOptions
            {
                ClientCert = clientCert,
                ClientPrivateKey = clientKey,
            };
        }

        TemporalClient client = await ConnectWithRetryAsync(options);

        await SearchAttributeRegistration.EnsureRegisteredAsync(client, _logger);

        return client;
    }

    private async Task<TemporalClient> ConnectWithRetryAsync(TemporalClientConnectOptions options)
    {
        var delay = InitialDelay;
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var client = await TemporalClient.ConnectAsync(options);
                _logger.LogInformation(
                    "Connected to Temporal at {Address} (namespace: {Namespace})",
                    _settings.ServerAddress, _settings.Namespace);
                return client;
            }
            catch (Exception ex) when (attempt < MaxRetries)
            {
                _logger.LogWarning(
                    "Temporal connection attempt {Attempt}/{Max} failed: {Message}. Retrying in {Delay}s...",
                    attempt, MaxRetries, ex.Message, delay.TotalSeconds);
                await Task.Delay(delay);
                delay *= 2;
            }
        }

        // Final attempt — let it throw
        return await TemporalClient.ConnectAsync(options);
    }

    private bool HasTlsConfiguration() =>
        _settings.TlsCertPem is not null
        || !string.IsNullOrEmpty(_settings.TlsCertPath);
}
