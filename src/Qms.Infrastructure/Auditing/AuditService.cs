using System.Text.Json;
using Qms.Application.Auditing;
using Qms.Domain.Auditing;
using Qms.Infrastructure.Persistence;

namespace Qms.Infrastructure.Auditing;

public sealed class AuditService(QmsDbContext db) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public async Task RecordAsync(
        string? actorId,
        string entity,
        string? entityId,
        string action,
        object? before = null,
        object? after = null,
        string? ip = null,
        string? origin = null,
        CancellationToken cancellationToken = default
    )
    {
        var log = new AuditLog(
            actorId,
            entity,
            entityId,
            action,
            before is null ? null : JsonSerializer.Serialize(before, JsonOptions),
            after is null ? null : JsonSerializer.Serialize(after, JsonOptions),
            ip,
            origin
        );
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync(cancellationToken);
    }
}
