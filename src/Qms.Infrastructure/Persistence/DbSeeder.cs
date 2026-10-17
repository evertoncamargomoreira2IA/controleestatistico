using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qms.Application.Auditing;
using Qms.Application.Settings;
using Qms.Domain.MasterData;
using Qms.Domain.Security;
using Qms.Domain.Settings;
using Qms.Infrastructure.Identity;

namespace Qms.Infrastructure.Persistence;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IHostEnvironment environment)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in Roles.All)
            if (!await roles.RoleExistsAsync(role))
                await roles.CreateAsync(new IdentityRole(role));

        if (!environment.IsDevelopment())
            return;

        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        if (!await db.SettingsVersions.AnyAsync())
        {
            var initial = new SettingsVersion { Version = 1, Comment = "Versão inicial" };
            foreach (
                var pair in new Dictionary<string, object>
                {
                    ["capability.cp"] = QualitySettings.Defaults.Cp,
                    ["capability.cpk"] = QualitySettings.Defaults.Cpk,
                    ["capability.pp"] = QualitySettings.Defaults.Pp,
                    ["capability.ppk"] = QualitySettings.Defaults.Ppk,
                    ["gage_rr.study_percent"] = QualitySettings.Defaults.StudyPercent,
                    ["gage_rr.tolerance_percent"] = QualitySettings.Defaults.TolerancePercent,
                    ["gage_rr.ndc_min"] = QualitySettings.Defaults.NdcMin,
                    ["kappa.kappa"] = QualitySettings.Defaults.Kappa,
                    ["kappa.effectiveness_min"] = QualitySettings.Defaults.EffectivenessMin,
                    ["kappa.miss_max"] = QualitySettings.Defaults.MissMax,
                    ["kappa.false_alarm_max"] = QualitySettings.Defaults.FalseAlarmMax,
                    ["chart.outside_3sigma"] = QualitySettings.Defaults.Outside3Sigma,
                    ["chart.same_side"] = QualitySettings.Defaults.SameSide,
                    ["chart.trend"] = QualitySettings.Defaults.Trend,
                    ["chart.spec_proximity"] = QualitySettings.Defaults.SpecProximity,
                    ["frequency.control_limits_days"] = QualitySettings.Defaults.ControlLimitsDays,
                    ["frequency.msa_validity_days"] = QualitySettings.Defaults.MsaValidityDays,
                }
            )
                initial.Values.Add(
                    new SettingValue
                    {
                        Key = pair.Key,
                        ValueJson = System.Text.Json.JsonSerializer.Serialize(
                            pair.Value,
                            new System.Text.Json.JsonSerializerOptions
                            {
                                PropertyNamingPolicy = System
                                    .Text
                                    .Json
                                    .JsonNamingPolicy
                                    .SnakeCaseLower,
                            }
                        ),
                    }
                );
            db.SettingsVersions.Add(initial);
            await db.SaveChangesAsync();
        }
        await SeedDemoMasterDataAsync(db);
        await ReferenceFixtures.SeedAsync(db);

        if (
            scope
                .ServiceProvider.GetRequiredService<IConfiguration>()
                .GetValue<bool>("Security:DemoAdminCredentials")
        )
        {
            await EnsureDemoAdminAsync(scope.ServiceProvider);
            return;
        }

        if (await db.Users.AnyAsync())
            return;

        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = "admin",
            FullName = "Administrador",
            IsActive = true,
            MustChangePassword = true,
        };
        var result = await manager.CreateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                string.Join("; ", result.Errors.Select(error => error.Description))
            );

        // Senha fraca usada exclusivamente para o administrador inicial de desenvolvimento.
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
        user.PasswordHash = hasher.HashPassword(user, "admin");
        var update = await manager.UpdateAsync(user);
        if (!update.Succeeded)
            throw new InvalidOperationException(
                string.Join("; ", update.Errors.Select(error => error.Description))
            );

        var roleResult = await manager.AddToRoleAsync(user, Roles.Admin);
        if (!roleResult.Succeeded)
            throw new InvalidOperationException(
                string.Join("; ", roleResult.Errors.Select(error => error.Description))
            );

        await scope
            .ServiceProvider.GetRequiredService<IAuditService>()
            .RecordAsync(
                user.Id,
                "user",
                user.Id,
                "user.create",
                after: new
                {
                    user.UserName,
                    role = Roles.Admin,
                    user.IsActive,
                },
                origin: "default_admin"
            );
    }

    private static async Task EnsureDemoAdminAsync(IServiceProvider services)
    {
        var manager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await manager.FindByNameAsync("admin");
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = "admin",
                FullName = "Administrador",
                IsActive = true,
            };
            var create = await manager.CreateAsync(user);
            if (!create.Succeeded)
                throw new InvalidOperationException(
                    string.Join("; ", create.Errors.Select(x => x.Description))
                );
            await manager.AddToRoleAsync(user, Roles.Admin);
        }
        var hasher = services.GetRequiredService<IPasswordHasher<ApplicationUser>>();
        if (
            !user.MustChangePassword
            && user.IsActive
            && user.PasswordHash is not null
            && hasher.VerifyHashedPassword(user, user.PasswordHash, "admin")
                != PasswordVerificationResult.Failed
        )
            return;
        user.PasswordHash = hasher.HashPassword(user, "admin");
        user.MustChangePassword = false;
        user.IsActive = true;
        user.AccessFailedCount = 0;
        user.LockoutEnd = null;
        user.SecurityStamp = Guid.NewGuid().ToString();
        var update = await manager.UpdateAsync(user);
        if (!update.Succeeded)
            throw new InvalidOperationException(
                string.Join("; ", update.Errors.Select(x => x.Description))
            );
        await services
            .GetRequiredService<IAuditService>()
            .RecordAsync(
                user.Id,
                "user",
                user.Id,
                "user.demo_credentials",
                after: new
                {
                    user.UserName,
                    Environment = "Development",
                    user.MustChangePassword,
                },
                origin: "demo_seed"
            );
    }

    /// <summary>
    /// Development only: the same sample hierarchy as the Python seed_demo.py, so a fresh
    /// install can be tried right away (Montagem / Produto A / Linha 1 / Posto 10 / TL-001).
    /// </summary>
    private static async Task SeedDemoMasterDataAsync(QmsDbContext db)
    {
        if (await db.Areas.AnyAsync())
            return;

        var area = new Area { Name = "Montagem" };
        db.Areas.Add(area);
        await db.SaveChangesAsync();
        var product = new Product { AreaId = area.Id, Name = "Produto A" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        var line = new Line { ProductId = product.Id, Name = "Linha 1" };
        db.Lines.Add(line);
        await db.SaveChangesAsync();
        var station = new Station { LineId = line.Id, Name = "Posto 10" };
        db.Stations.Add(station);
        await db.SaveChangesAsync();
        var tooling = new Tooling
        {
            StationId = station.Id,
            Code = "TL-001",
            Name = "TL-001",
            Description = "Tooling de demonstração",
        };
        db.Toolings.Add(tooling);
        await db.SaveChangesAsync();
        db.Characteristics.AddRange(
            new Characteristic
            {
                ToolingId = tooling.Id,
                Name = "Altura",
                Unit = "mm",
                Lsl = 9.5,
                Nominal = 10,
                Usl = 10.5,
                SubgroupSize = 3,
                ChartType = ChartType.XbarR,
            },
            new Characteristic
            {
                ToolingId = tooling.Id,
                Name = "Inspeção visual",
                DataType = CharacteristicDataType.Attribute,
                SubgroupSize = 1,
                ChartType = ChartType.IMr,
            }
        );
        db.Instruments.Add(
            new Instrument
            {
                Code = "INS-001",
                Name = "Instrumento 001",
                Type = "Paquímetro",
                Resolution = 0.01,
                CalibrationDue = DateOnly.FromDateTime(DateTime.Today.AddYears(1)),
            }
        );
        await db.SaveChangesAsync();
    }
}
