namespace Ascend.Api.Events;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";
    public bool Enabled { get; set; } = true;
    public string BootstrapServers { get; set; } = string.Empty;
    public string Topic { get; set; } = "ascend-events";
    public string ConnectionString { get; set; } = string.Empty;
    public int MessageTimeoutMs { get; set; } = 10_000;
}
