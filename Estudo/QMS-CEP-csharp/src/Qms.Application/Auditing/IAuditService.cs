namespace Qms.Application.Auditing;

public interface IAuditService
{
    Task RecordAsync(
        string? actorId,
        string entity,
        string? entityId,
        string action,
        object? before = null,
        object? after = null,
        string? ip = null,
        string? origin = null,
        CancellationToken cancellationToken = default
    );
}
