namespace Qms.Domain.Collection;

public sealed class Subgroup
{
    public int Id { get; set; }
    public int CharacteristicId { get; set; }
    public int CharacteristicRevision { get; set; }
    public int? InstrumentId { get; set; }
    public string OperatorId { get; set; } = "";
    public DateTimeOffset CollectedAt { get; set; }
    public string? Shift { get; set; }
    public string? Lot { get; set; }
    public string Source { get; set; } = "online";
    public int? ImportBatchId { get; set; }
    public bool IsExcluded { get; set; }
    public string? ExclusionReason { get; set; }
    public string? Note { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<Measurement> Measurements { get; set; } = [];
}

public sealed class Measurement
{
    public int Id { get; set; }
    public int SubgroupId { get; set; }
    public int Position { get; set; }
    public double Value { get; set; }
}

public sealed class ImportBatch
{
    public int Id { get; set; }
    public string FileName { get; set; } = "";
    public string UploadedBy { get; set; } = "";
    public DateTimeOffset UploadedAt { get; set; }
    public int RowsTotal { get; set; }
    public int RowsImported { get; set; }
    public int RowsRejected { get; set; }
    public string FileSha256 { get; set; } = "";
    public string Status { get; set; } = "";
    public List<Subgroup> Subgroups { get; set; } = [];
}
