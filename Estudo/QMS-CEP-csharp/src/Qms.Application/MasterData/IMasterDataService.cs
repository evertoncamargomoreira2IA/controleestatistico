using Qms.Domain.MasterData;

namespace Qms.Application.MasterData;

public sealed record OperationResult<T>(bool Succeeded, T? Value, string? Error)
{
    public static OperationResult<T> Success(T value) => new(true, value, null);

    public static OperationResult<T> Failure(string error) => new(false, default, error);
}

public interface IMasterDataService
{
    Task<IReadOnlyList<T>> ListChildrenAsync<T>(
        int parentId,
        CancellationToken cancellationToken = default
    )
        where T : MasterDataEntity;
    Task<OperationResult<T>> CreateAsync<T>(
        T entity,
        string? actorId,
        CancellationToken cancellationToken = default
    )
        where T : MasterDataEntity;
    Task<OperationResult<T>> UpdateAsync<T>(
        T entity,
        string? actorId,
        CancellationToken cancellationToken = default
    )
        where T : MasterDataEntity;
    Task<IReadOnlyList<string>> GetBreadcrumbAsync(
        string entity,
        int id,
        CancellationToken cancellationToken = default
    );
}

public interface IInstrumentService
{
    Task<IReadOnlyList<Instrument>> ListAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<Instrument>> SaveAsync(
        Instrument instrument,
        string? actorId,
        CancellationToken cancellationToken = default
    );
}
