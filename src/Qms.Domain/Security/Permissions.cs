namespace Qms.Domain.Security;

public static class Permissions
{
    public const string View = "view";
    public const string UsersManage = "users.manage";
    public const string SettingsManage = "settings.manage";
    public const string MasterdataManage = "masterdata.manage";
    public const string CollectionCreate = "collection.create";
    public const string StudiesRun = "studies.run";
    public const string AuditView = "audit.view";

    public static IReadOnlyList<string> All { get; } =
    [
        View,
        UsersManage,
        SettingsManage,
        MasterdataManage,
        CollectionCreate,
        StudiesRun,
        AuditView,
    ];

    public static IReadOnlyDictionary<string, IReadOnlySet<string>> RolePermissions { get; } =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [Roles.Admin] = new HashSet<string>(All, StringComparer.Ordinal),
            [Roles.Engenheiro] = new HashSet<string>(
                [View, CollectionCreate, StudiesRun, MasterdataManage],
                StringComparer.Ordinal
            ),
            [Roles.Tecnico] = new HashSet<string>([View, CollectionCreate], StringComparer.Ordinal),
            [Roles.Qualidade] = new HashSet<string>(
                [View, StudiesRun, AuditView],
                StringComparer.Ordinal
            ),
            [Roles.Leitor] = new HashSet<string>([View], StringComparer.Ordinal),
        };

    public static bool Allows(string? role, string permission) =>
        role is not null
        && RolePermissions.TryGetValue(role, out var grants)
        && grants.Contains(permission);
}
