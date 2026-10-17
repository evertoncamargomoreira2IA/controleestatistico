namespace Qms.Application.Settings;

public sealed record SettingsHistory(
    int Version,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    string Comment,
    IReadOnlyDictionary<string, string> Diff
);

public interface ISettingsService
{
    Task<QualitySettings> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<(bool Succeeded, int? Version, string? Error)> SaveAsync(
        QualitySettings settings,
        string? comment,
        string? actorId,
        CancellationToken cancellationToken = default
    );
    Task<IReadOnlyList<SettingsHistory>> GetHistoryAsync(
        CancellationToken cancellationToken = default
    );
}
