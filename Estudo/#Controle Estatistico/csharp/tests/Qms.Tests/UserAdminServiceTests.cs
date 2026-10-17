using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qms.Application.Auditing;
using Qms.Application.Users;
using Qms.Domain.Security;
using Qms.Infrastructure.Identity;
using Qms.Infrastructure.Persistence;

namespace Qms.Tests;

public sealed class UserAdminServiceTests : IDisposable
{
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private readonly ServiceProvider services;
    private readonly QmsDbContext db;
    private readonly UserManager<ApplicationUser> users;
    private readonly UserAdminService service;
    private readonly RecordingAudit audit;

    public UserAdminServiceTests()
    {
        connection.Open();
        var collection = new ServiceCollection();
        collection.AddLogging();
        collection.AddDataProtection();
        collection.AddDbContext<QmsDbContext>(options => options.UseSqlite(connection));
        collection
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireDigit = true;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<QmsDbContext>()
            .AddDefaultTokenProviders();
        collection.AddSingleton<RecordingAudit>();
        collection.AddSingleton<IAuditService>(provider =>
            provider.GetRequiredService<RecordingAudit>()
        );
        services = collection.BuildServiceProvider();
        db = services.GetRequiredService<QmsDbContext>();
        db.Database.EnsureCreated();
        users = services.GetRequiredService<UserManager<ApplicationUser>>();
        audit = services.GetRequiredService<RecordingAudit>();
        service = new UserAdminService(
            users,
            services.GetRequiredService<RoleManager<IdentityRole>>(),
            db,
            audit
        );
        foreach (var role in Roles.All)
            services
                .GetRequiredService<RoleManager<IdentityRole>>()
                .CreateAsync(new IdentityRole(role))
                .GetAwaiter()
                .GetResult();
    }

    [Fact]
    public async Task Creates_user_and_records_audit()
    {
        var result = await service.CreateAsync(
            "actor",
            new UserChange("maria", "Maria Silva", Roles.Tecnico, "SenhaSegura2026")
        );

        Assert.True(result.Succeeded, result.Error);
        var user = await users.FindByNameAsync("maria");
        Assert.NotNull(user);
        Assert.True(user!.MustChangePassword);
        Assert.Contains(Roles.Tecnico, await users.GetRolesAsync(user));
        Assert.Contains(
            audit.Entries,
            entry => entry.Action == "user.create" && entry.After is not null
        );
    }

    [Fact]
    public async Task Cannot_deactivate_self()
    {
        var admin = await CreateAdmin("admin1");
        var result = await service.UpdateAsync(
            admin.Id,
            admin.Id,
            new UserChange("admin1", admin.FullName, Roles.Admin, IsActive: false)
        );

        Assert.False(result.Succeeded);
        Assert.Contains("próprio", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cannot_demote_last_active_admin()
    {
        var admin = await CreateAdmin("admin1");
        var result = await service.UpdateAsync(
            "other",
            admin.Id,
            new UserChange("admin1", admin.FullName, Roles.Leitor)
        );

        Assert.False(result.Succeeded);
        Assert.Contains("último administrador", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Password_reset_sets_change_flag_and_clears_lockout()
    {
        var admin = await CreateAdmin("admin1");
        var user = new ApplicationUser
        {
            UserName = "maria",
            FullName = "Maria Silva",
            IsActive = true,
            LockoutEnd = DateTimeOffset.UtcNow.AddHours(1),
            AccessFailedCount = 3,
        };
        Assert.True((await users.CreateAsync(user, "SenhaSegura2026")).Succeeded);
        await users.AddToRoleAsync(user, Roles.Tecnico);

        var result = await service.UpdateAsync(
            admin.Id,
            user.Id,
            new UserChange(
                "maria",
                user.FullName,
                Roles.Tecnico,
                IsActive: true,
                ResetPassword: true,
                NewPassword: "OutraSenhaSegura2026"
            )
        );

        Assert.True(result.Succeeded, result.Error);
        var updated = await users.FindByIdAsync(user.Id);
        Assert.True(updated!.MustChangePassword);
        Assert.Null(updated.LockoutEnd);
        Assert.Equal(0, updated.AccessFailedCount);
        Assert.Contains(audit.Entries, entry => entry.Action == "user.password_reset");
    }

    private async Task<ApplicationUser> CreateAdmin(string name)
    {
        var user = new ApplicationUser
        {
            UserName = name,
            FullName = "Administrador",
            IsActive = true,
        };
        Assert.True((await users.CreateAsync(user, "SenhaSegura2026")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, Roles.Admin)).Succeeded);
        return user;
    }

    public void Dispose()
    {
        services.Dispose();
        connection.Dispose();
    }

    private sealed class RecordingAudit : IAuditService
    {
        public List<AuditEntry> Entries { get; } = [];

        public Task RecordAsync(
            string? actorId,
            string entity,
            string? entityId,
            string action,
            object? before = null,
            object? after = null,
            string? ip = null,
            string? origin = null,
            CancellationToken cancellationToken = default
        )
        {
            Entries.Add(new AuditEntry(action, before, after));
            return Task.CompletedTask;
        }
    }

    private sealed record AuditEntry(string Action, object? Before, object? After);
}
