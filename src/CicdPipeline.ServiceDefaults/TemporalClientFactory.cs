using System.Text;
using Microsoft.Extensions.Options;
using Temporalio.Client;

namespace CicdPipeline.ServiceDefaults;

public class TemporalClientFactory
{
    private readonly TemporalSettings _settings;

    public TemporalClientFactory(IOptions<TemporalSettings> settings)
    {
        _settings = settings.Value;
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

        return await TemporalClient.ConnectAsync(options);
    }

    private bool HasTlsConfiguration() =>
        _settings.TlsCertPem is not null
        || !string.IsNullOrEmpty(_settings.TlsCertPath);
}
