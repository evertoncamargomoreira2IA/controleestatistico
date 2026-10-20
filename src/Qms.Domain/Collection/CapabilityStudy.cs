namespace Qms.Domain.Collection;

public sealed class CapabilityStudy
{
    public int Id { get; set; }
    public int DatasetId { get; set; }
    public int DatasetVersion { get; set; }
    public int SettingsVersion { get; set; }
    public string Method { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedBy { get; set; } = "";
    public string InputJson { get; set; } = "";
    public string ResultJson { get; set; } = "";
}
