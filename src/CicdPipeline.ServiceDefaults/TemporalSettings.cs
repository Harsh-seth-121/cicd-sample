namespace CicdPipeline.ServiceDefaults;

public class TemporalSettings
{
    public string ServerAddress { get; set; } = "localhost:7233";
    public string Namespace { get; set; } = "cicd-prodctl";
    public string TlsCertPath { get; set; } = "";
    public string TlsKeyPath { get; set; } = "";
    public string? TlsCertPem { get; set; }
    public string? TlsKeyPem { get; set; }
}
