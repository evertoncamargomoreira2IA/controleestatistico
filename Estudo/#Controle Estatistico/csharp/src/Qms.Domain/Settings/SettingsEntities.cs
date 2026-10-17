namespace Qms.Domain.Settings;

public sealed class SettingsVersion
{
    public int Id { get; set; }
    public int Version { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? CreatedBy { get; set; }
    public string Comment { get; set; } = "";
    public List<SettingValue> Values { get; set; } = [];
}

public sealed class SettingValue
{
    public int Id { get; set; }
    public int SettingsVersionId { get; set; }
    public string Key { get; set; } = "";
    public string ValueJson { get; set; } = "";
}
