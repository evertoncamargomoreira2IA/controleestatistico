using System.Security.Claims;
using Qms.Domain.Collection;
using Qms.Domain.MasterData;

namespace Qms.Application.Collection;

public sealed record CepChoice(Characteristic Characteristic, string ProcessPath);

public sealed record CepRowInput(
    int Position,
    string? Value1,
    string? Value2,
    string? Value3,
    int? InstrumentId,
    DateTimeOffset? CollectedAt,
    string Shift,
    string Lot,
    int? SourceSubgroupId = null
);

public sealed record CepSaveInput(
    int Id,
    int Version,
    string Title,
    IReadOnlyList<CepRowInput> Rows,
    bool Complete
);

public sealed record CepHistoryItem(
    DateTimeOffset At,
    string Actor,
    string Action,
    string? Before,
    string? After
);

public sealed record CepSourceRow(Subgroup Subgroup, string Instrument, string Operator);

public sealed record CepDaily(DateOnly Day, IReadOnlyList<CepDataset> Datasets);

public interface ICepDatasetService
{
    Task<IReadOnlyList<CepChoice>> ChoicesAsync(ClaimsPrincipal user);
    Task<IReadOnlyList<Instrument>> InstrumentsAsync(ClaimsPrincipal user);
    Task<CepDataset> CreateAsync(int characteristicId, string title, ClaimsPrincipal user);
    Task<CepDataset> GetAsync(int id, ClaimsPrincipal user);
    Task<CepDataset> SaveAsync(CepSaveInput input, ClaimsPrincipal user);
    Task<IReadOnlyList<CepSourceRow>> SourcesAsync(
        int datasetId,
        DateTimeOffset from,
        DateTimeOffset until,
        string shift,
        string lot,
        ClaimsPrincipal user
    );
    Task<IReadOnlyList<CepHistoryItem>> HistoryAsync(int id, ClaimsPrincipal user);
    Task<CepDaily> DailyAsync(DateOnly day, ClaimsPrincipal user);
}
