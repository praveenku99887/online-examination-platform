using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace Ascend.Api.Events;

public sealed record AscendEvent(string EventType, string EntityId, DateTimeOffset OccurredAt, object Data);

public static class AscendEventTypes
{
    public const string UserSignedUp = "user.signedup";
    public const string UserLoggedIn = "user.loggedin";
    public const string AssessmentCreated = "assessment.created";
    public const string AssessmentDeleted = "assessment.deleted";
    public const string AttemptStarted = "attempt.started";
    public const string AttemptSubmitted = "attempt.submitted";
    public const string AiDraftGenerated = "ai.draft.generated";
}

public interface IAscendEventPublisher
{
    void Publish(string eventType, string entityId, object data);
}

public sealed class KafkaAscendEventPublisher : IAscendEventPublisher, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private readonly ILogger<KafkaAscendEventPublisher> _logger;
    private readonly IProducer<string, string>? _producer;
    private readonly string _topic;

    public KafkaAscendEventPublisher(IOptions<KafkaOptions> options, ILogger<KafkaAscendEventPublisher> logger)
    {
        _logger = logger;
        var settings = options.Value;
        _topic = settings.Topic;

        if (!settings.Enabled)
        {
            _logger.LogInformation("Kafka publishing is disabled (Kafka:Enabled = false).");
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.BootstrapServers))
        {
            _logger.LogWarning("Kafka:BootstrapServers is not configured; ascend events will not be published.");
            return;
        }

        var config = new ProducerConfig
        {
            BootstrapServers = settings.BootstrapServers,
            MessageTimeoutMs = settings.MessageTimeoutMs,
            Acks = Acks.All,
            QueueBufferingMaxMessages = 100_000,
        };

        if (!string.IsNullOrWhiteSpace(settings.ConnectionString))
        {
            config.SecurityProtocol = SecurityProtocol.SaslSsl;
            config.SaslMechanism = SaslMechanism.Plain;
            config.SaslUsername = "$ConnectionString";
            config.SaslPassword = settings.ConnectionString;
        }

        try
        {
            _producer = new ProducerBuilder<string, string>(config).Build();
            _logger.LogInformation("Kafka publisher ready. bootstrap={BootstrapServers} topic={Topic}", settings.BootstrapServers, settings.Topic);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not create the Kafka producer; ascend events will not be published.");
        }
    }

    public void Publish(string eventType, string entityId, object data)
    {
        var producer = _producer;
        if (producer is null) return;

        var envelope = new AscendEvent(eventType, entityId, DateTimeOffset.UtcNow, data);
        string payload;
        try
        {
            payload = JsonSerializer.Serialize(envelope, JsonOptions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not serialise {EventType} for entity {EntityId}.", eventType, entityId);
            return;
        }

        try
        {
            producer.Produce(_topic, new Message<string, string> { Key = entityId, Value = payload }, report =>
            {
                if (report.Error.IsError)
                    _logger.LogError("Kafka delivery failed for {EventType} entity {EntityId}: {Reason}", eventType, entityId, report.Error.Reason);
                else
                    _logger.LogInformation("Published {EventType} for entity {EntityId} to {Topic}[{Partition}]@{Offset}", eventType, entityId, report.Topic, report.Partition.Value, report.Offset.Value);
            });
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError("Kafka rejected {EventType} for entity {EntityId}: {Reason}", eventType, entityId, ex.Error.Reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected Kafka failure publishing {EventType} for entity {EntityId}.", eventType, entityId);
        }
    }

    public void Dispose()
    {
        if (_producer is null) return;
        try { _producer.Flush(TimeSpan.FromSeconds(5)); }
        catch (Exception ex) { _logger.LogWarning(ex, "Kafka flush during shutdown did not complete cleanly."); }
        finally { _producer.Dispose(); }
    }
}
