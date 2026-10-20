namespace Qms.Domain.Collection;

public sealed class CepPlan
{
    public int Id { get; set; }
    public int CharacteristicId { get; set; }
    public int CharacteristicRevision { get; set; }
    public string ProcessPath { get; set; } = "";
    public string CharacteristicName { get; set; } = "";
    public string Day { get; set; } = "";
    public string Shift { get; set; } = "T1";
    public string Title { get; set; } = "";
    public string ResponsibleId { get; set; } = "";
    public int TargetSets { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public bool Cancelled { get; set; }
    public int Version { get; set; } = 1;
}

public sealed class CepPlanDataset
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public int DatasetId { get; set; }
    public DateTimeOffset LinkedAt { get; set; }
    public string LinkedBy { get; set; } = "";
}

public sealed class CepLimitApproval
{
    public int Id { get; set; }
    public int SourceAnalysisId { get; set; }
    public int CharacteristicId { get; set; }
    public int CharacteristicRevision { get; set; }
    public string ResponsibleId { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset ApprovedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string SourceResultJson { get; set; } = "";
}

// Decisions are append-only: revocation never edits the original approval.
public sealed class CepLimitRevocation
{
    public int Id { get; set; }
    public int ApprovalId { get; set; }
    public string ActorId { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTimeOffset At { get; set; }
}

public sealed class CepOccurrence
{
    public int Id { get; set; }
    public int AnalysisId { get; set; }
    public string Description { get; set; } = "";
    public string ResponsibleId { get; set; } = "";
    public DateTimeOffset DueAt { get; set; }
    public string Status { get; set; } = "Aberta";
    public string Cause { get; set; } = "";
    public string Action { get; set; } = "";
    public string Verification { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public int Version { get; set; } = 1;
}
