namespace Qms.Application.Users;

public sealed record UserRecord(
    string Id,
    string UserName,
    string FullName,
    string Role,
    bool IsActive,
    DateTimeOffset? LockoutEnd,
    DateTimeOffset? LastLoginAt
);

public sealed record UserChange(
    string UserName,
    string FullName,
    string Role,
    string? InitialPassword = null,
    bool IsActive = true,
    bool ResetPassword = false,
    string? NewPassword = null
);

public sealed record UserOperation(bool Succeeded, string? Error = null);

public interface IUserAdminService
{
    Task<IReadOnlyList<UserRecord>> ListAsync(
        string? search = null,
        CancellationToken cancellationToken = default
    );
    Task<UserOperation> CreateAsync(
        string actorId,
        UserChange change,
        string? ip = null,
        CancellationToken cancellationToken = default
    );
    Task<UserOperation> UpdateAsync(
        string actorId,
        string userId,
        UserChange change,
        string? ip = null,
        CancellationToken cancellationToken = default
    );
}
