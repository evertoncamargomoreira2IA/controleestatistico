using System.Security.Claims;
using Qms.Domain.Collection;

namespace Qms.Application.Collection;

public sealed record CepOperator(string Id, string Name);

public sealed record CepPlanInput(
    int CharacteristicId,
    string Day,
    string Shift,
    string Title,
    string ResponsibleId,
    int TargetSets,
    DateTimeOffset DueAt
);

public sealed record CepPlanProgress(
    CepPlan Plan,
    int Linked,
    int Complete,
    int Analyzed,
    int OpenOccurrences,
    string Status,
    IReadOnlyList<int> DatasetIds
)
{
    public int CollectionPercent => Math.Min(100, Complete * 100 / Plan.TargetSets);
}

public sealed record CepOccurrenceInput(
    int Id,
    int Version,
    string ResponsibleId,
    DateTimeOffset DueAt,
    string Status,
    string Cause,
    string Action,
    string Verification
);

public sealed record CepOperationEvent(
    DateTimeOffset At,
    string Actor,
    string Action,
    string? Before,
    string? After
);

public sealed record CepOperationsBoard(
    IReadOnlyList<CepPlanProgress> Plans,
    IReadOnlyList<CepOccurrence> Occurrences,
    IReadOnlyList<CepLimitApproval> Approvals,
    IReadOnlyList<int> RevokedApprovalIds,
    IReadOnlyList<CepOperator> Operators
);

public interface ICepOperationsService
{
    Task<CepOperationsBoard> BoardAsync(string day, ClaimsPrincipal user);
    Task<CepPlan> PlanAsync(CepPlanInput input, ClaimsPrincipal user);
    Task LinkAsync(int planId, int datasetId, int version, ClaimsPrincipal user);
    Task CancelAsync(int planId, int version, string reason, ClaimsPrincipal user);
    Task<CepLimitApproval> ApproveAsync(
        int analysisId,
        int validityDays,
        string reason,
        ClaimsPrincipal user
    );
    Task RevokeAsync(int approvalId, string reason, ClaimsPrincipal user);
    Task<CepOccurrence> OpenAsync(
        int analysisId,
        string description,
        string responsibleId,
        DateTimeOffset dueAt,
        ClaimsPrincipal user
    );
    Task UpdateAsync(CepOccurrenceInput input, ClaimsPrincipal user);
    Task<IReadOnlyList<CepOperationEvent>> HistoryAsync(
        string entity,
        int id,
        ClaimsPrincipal user
    );
}
