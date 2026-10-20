using Qms.Domain.Security;

namespace Qms.Tests;

public sealed class RolePermissionsTests
{
    [Fact]
    public void Role_permissions_match_the_f0_matrix()
    {
        Assert.True(
            Permissions.All.ToHashSet().SetEquals(Permissions.RolePermissions[Roles.Admin])
        );
        Assert.True(
            new HashSet<string>
            {
                "view",
                "collection.create",
                "studies.run",
                "masterdata.manage",
                "cep.plan.manage",
                "cep.actions.manage",
            }.SetEquals(Permissions.RolePermissions[Roles.Engenheiro])
        );
        Assert.True(
            new HashSet<string> { "view", "collection.create", "cep.actions.manage" }.SetEquals(
                Permissions.RolePermissions[Roles.Tecnico]
            )
        );
        Assert.True(
            new HashSet<string> { "view", "studies.run", "audit.view", "cep.plan.manage", "cep.limits.approve", "cep.actions.manage" }.SetEquals(
                Permissions.RolePermissions[Roles.Qualidade]
            )
        );
        Assert.True(
            new HashSet<string> { "view" }.SetEquals(Permissions.RolePermissions[Roles.Leitor])
        );
    }

    [Fact]
    public void Admin_has_every_permission_and_leitor_only_view()
    {
        Assert.All(
            Permissions.All,
            permission => Assert.True(Permissions.Allows(Roles.Admin, permission))
        );
        Assert.True(Permissions.Allows(Roles.Leitor, Permissions.View));
        Assert.All(
            Permissions.All.Where(permission => permission != Permissions.View),
            permission => Assert.False(Permissions.Allows(Roles.Leitor, permission))
        );
    }
}
