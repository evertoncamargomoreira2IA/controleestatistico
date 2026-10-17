namespace Qms.Domain.Auditing;

public sealed class AuditLog
{
    public long Id { get; private set; }
    public string? ActorId { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; } = DateTimeOffset.UtcNow;
    public string Entity { get; private set; } = string.Empty;
    public string? EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public string? IpAddress { get; private set; }
    public string? Origin { get; private set; }

    private AuditLog() { }

    public AuditLog(
        string? actorId,
        string entity,
        string? entityId,
        string action,
        string? beforeJson,
        string? afterJson,
        string? ipAddress,
        string? origin
    )
    {
        ActorId = actorId;
        Entity = entity;
        EntityId = entityId;
        Action = action;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        IpAddress = ipAddress;
        Origin = origin;
        OccurredAt = DateTimeOffset.UtcNow;
    }
}
