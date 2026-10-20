using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qms.Application.Collection;
using Qms.Application.Settings;
using Qms.Domain.Collection;
using Qms.Domain.Security;
using Qms.Infrastructure.Identity;
using Qms.Infrastructure.Persistence;
using Qms.Statistics;

namespace Qms.Tests;

public sealed partial class CepDatasetTests
{
    [Fact]
    public async Task Rule_versions_create_distinct_snapshots_without_changing_prior_analysis()
    {
        await Operations();var e=User(Roles.Engenheiro);var analyses=services.GetRequiredService<ICepAnalysisService>();
        var set=await Create();await service.SaveAsync(new(set.Id,1,set.Title,Rows(),true),technician);
        var before=await analyses.RunAsync(set.Id,e,null,true);
        using(var scope=services.CreateScope()) {
            var settings=scope.ServiceProvider.GetRequiredService<ISettingsService>();
            var saved=await settings.SaveAsync(QualitySettings.Defaults with {SameSide=new(false,9),Trend=new(false,6)},"Revisão de regras de teste","operator-1");
            Assert.True(saved.Succeeded);
        }
        var after=await analyses.RunAsync(set.Id,e,null,true);
        Assert.NotEqual(before.Id,after.Id);Assert.NotEqual(before.Method,after.Method);
        Assert.Equal(before.ResultJson,(await analyses.GetAsync(before.Id,User(Roles.Leitor))).ResultJson);
        var result=JsonSerializer.Deserialize<CepResult>(after.ResultJson)!;Assert.False(result.Rules!.SameSideEnabled);
        Assert.Equal(2,(await analyses.ListAsync(set.Id,e)).Count);
    }
    private async Task<ICepOperationsService> Operations()
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        db.Users.Add(
            new ApplicationUser
            {
                Id = "operator-1",
                UserName = "operator",
                NormalizedUserName = "OPERATOR",
                FullName = "Operador de teste",
                IsActive = true,
                SecurityStamp = Guid.NewGuid().ToString(),
            }
        );
        await db.SaveChangesAsync();
        return services.GetRequiredService<ICepOperationsService>();
    }

    private CepPlanInput PlanInput(int target = 1)
    {
        var local = LocalTime.ToLocal(DateTimeOffset.UtcNow);
        return new(
            characteristicId,
            local.ToString("yyyy-MM-dd"),
            "T1",
            "Plano de teste",
            "operator-1",
            target,
            LocalTime.ParseLocal(local.ToString("yyyy-MM-dd") + "T23:59")
        );
    }

    [Fact]
    public async Task Plan_links_once_tracks_collection_analysis_and_investigation_with_history()
    {
        var op = await Operations();
        var engineer = User(Roles.Engenheiro);
        var plan = await op.PlanAsync(PlanInput(), engineer);
        var set = await Create();
        await op.LinkAsync(plan.Id, set.Id, plan.Version, technician);
        var board = await op.BoardAsync(plan.Day, technician);
        Assert.Equal("Em coleta", Assert.Single(board.Plans).Status);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            op.LinkAsync(plan.Id, set.Id, 2, technician)
        );
        await service.SaveAsync(new(set.Id, set.Version, set.Title, Rows(), true), technician);
        var analysis = await services
            .GetRequiredService<ICepAnalysisService>()
            .RunAsync(set.Id, engineer);
        Assert.Equal(
            "Concluída",
            Assert.Single((await op.BoardAsync(plan.Day, technician)).Plans).Status
        );
        var occurrence = await op.OpenAsync(
            analysis.Id,
            "Verificar ajuste do dispositivo",
            "operator-1",
            DateTimeOffset.UtcNow.AddHours(4),
            technician
        );
        Assert.NotEqual(
            "Concluída",
            Assert.Single((await op.BoardAsync(plan.Day, technician)).Plans).Status
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            op.UpdateAsync(
                new(occurrence.Id, 1, "operator-1", occurrence.DueAt, "Encerrada", "", "", ""),
                technician
            )
        );
        await op.UpdateAsync(
            new(
                occurrence.Id,
                1,
                "operator-1",
                occurrence.DueAt,
                "Encerrada",
                "Ajuste identificado",
                "Dispositivo ajustado",
                "Nova verificação registrada"
            ),
            technician
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            op.UpdateAsync(
                new(occurrence.Id, 1, "operator-1", occurrence.DueAt, "Aberta", "", "", ""),
                technician
            )
        );
        Assert.Equal(
            "Concluída",
            Assert.Single((await op.BoardAsync(plan.Day, technician)).Plans).Status
        );
        Assert.Equal(2, (await op.HistoryAsync("cep_plan", plan.Id, technician)).Count);
        Assert.Equal(2, (await op.HistoryAsync("cep_occurrence", occurrence.Id, technician)).Count);
        Assert.Equal(
            100,
            Assert.Single((await op.BoardAsync(plan.Day, technician)).Plans).CollectionPercent
        );
    }

    [Fact]
    public async Task Cancelled_and_stale_plans_reject_links_and_preserve_records()
    {
        var op = await Operations();
        var engineer = User(Roles.Engenheiro);
        var plan = await op.PlanAsync(PlanInput(), engineer);
        var set = await Create();
        await Assert.ThrowsAsync<ArgumentException>(() => op.CancelAsync(plan.Id, 1, "", engineer));
        await op.CancelAsync(plan.Id, 1, "Reprogramação do turno", engineer);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            op.LinkAsync(plan.Id, set.Id, 1, technician)
        );
        Assert.Equal(
            "Cancelada",
            Assert.Single((await op.BoardAsync(plan.Day, technician)).Plans).Status
        );
        Assert.Equal(2, (await op.HistoryAsync("cep_plan", plan.Id, technician)).Count);
    }

    [Fact]
    public async Task Plan_rejects_invalid_metadata_inactive_owner_and_wrong_shift()
    {
        var op = await Operations();
        var e = User(Roles.Engenheiro);
        var input = PlanInput();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            op.PlanAsync(input with { TargetSets = 0 }, e)
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            op.PlanAsync(input with { Day = "2026-02-30" }, e)
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            op.PlanAsync(input with { ResponsibleId = "absent" }, e)
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            op.PlanAsync(input with { DueAt = input.DueAt.AddDays(1) }, e)
        );
        var plan = await op.PlanAsync(input with { Shift = "T2" }, e);
        var set = await Create();
        await service.SaveAsync(new(set.Id, set.Version, set.Title, Rows(), true), technician);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            op.LinkAsync(plan.Id, set.Id, 1, technician)
        );
        Assert.Equal(0, Assert.Single((await op.BoardAsync(plan.Day, e)).Plans).Complete);
    }

    [Fact]
    public async Task Approved_limits_are_frozen_applied_to_later_data_and_revocation_blocks_new_runs()
    {
        var op = await Operations();
        var e = User(Roles.Engenheiro);
        var q = User(Roles.Qualidade);
        var analyses = services.GetRequiredService<ICepAnalysisService>();
        var source = await Create();
        await service.SaveAsync(new(source.Id, 1, source.Title, Rows(), true), technician);
        var baseline = await analyses.RunAsync(source.Id, e);
        var approval = await op.ApproveAsync(
            baseline.Id,
            180,
            "Base revisada em reunião de processo",
            q
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            op.ApproveAsync(baseline.Id, 180, "Repetida", q)
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            analyses.RunAsync(source.Id, e, approval.Id, true)
        );
        var targetSet = await Create();
        var rows = Rows()
            .Select(x =>
                x with
                {
                    Value1 = "10.19",
                    Value2 = "10.21",
                    Value3 = "10.20",
                    CollectedAt = DateTimeOffset.UtcNow,
                }
            )
            .ToArray();
        await service.SaveAsync(new(targetSet.Id, 1, "Monitoramento", rows, true), technician);
        var monitored = await analyses.RunAsync(targetSet.Id, e, approval.Id, true);
        var result = JsonSerializer.Deserialize<CepResult>(monitored.ResultJson)!;
        Assert.Equal(approval.Id, result.ApprovedLimitId);
        Assert.Equal(10.03, result.ControlCenter!.Value, 10);
        Assert.Equal(25, result.Signals.Count(x => x.Code == "x-outside"));
        var repeat = await analyses.RunAsync(targetSet.Id, e, approval.Id, true);
        Assert.Equal(monitored.Id, repeat.Id);
        var board = await op.BoardAsync(PlanInput().Day, e);
        Assert.Single(board.Occurrences);
        Assert.Equal(monitored.Id, board.Occurrences[0].AnalysisId);
        await op.RevokeAsync(approval.Id, "Revisão do processo", q);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            analyses.RunAsync(targetSet.Id, e, approval.Id, true)
        );
        Assert.Equal(
            monitored.ResultJson,
            (await analyses.GetAsync(monitored.Id, User(Roles.Leitor))).ResultJson
        );
        Assert.Equal(2, (await op.HistoryAsync("cep_limit", approval.Id, technician)).Count);
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        var frozen = await db.CepLimitApprovals.SingleAsync();
        frozen.Reason = "alteração";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Approval_rejects_signals_zero_range_open_investigation_and_expired_readings()
    {
        var op = await Operations();
        var e = User(Roles.Engenheiro);
        var q = User(Roles.Qualidade);
        var analyses = services.GetRequiredService<ICepAnalysisService>();
        var source = await Create();
        await service.SaveAsync(new(source.Id, 1, source.Title, Rows(), true), technician);
        var baseline = await analyses.RunAsync(source.Id, e);
        var occurrence = await op.OpenAsync(
            baseline.Id,
            "Análise de causa em aberto",
            "operator-1",
            DateTimeOffset.UtcNow.AddDays(1),
            e
        );
        await Assert.ThrowsAsync<ArgumentException>(() =>
            op.ApproveAsync(baseline.Id, 180, "Justificativa", q)
        );
        await op.UpdateAsync(
            new(
                occurrence.Id,
                1,
                "operator-1",
                occurrence.DueAt,
                "Encerrada",
                "Causa",
                "Ação",
                "Verificação"
            ),
            e
        );
        var approval = await op.ApproveAsync(baseline.Id, 180, "Investigação concluída", q);
        var earlier = await Create();
        await service.SaveAsync(new(earlier.Id, 1, earlier.Title, Rows(), true), technician);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            analyses.RunAsync(earlier.Id, e, approval.Id, true)
        );
        var zero = await Create();
        var values = Rows()
            .Select(x => x with { Value1 = "10", Value2 = "10", Value3 = "10" })
            .ToArray();
        await service.SaveAsync(new(zero.Id, 1, zero.Title, values, true), technician);
        var zeroAnalysis = await analyses.RunAsync(zero.Id, e, null, true);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            op.ApproveAsync(zeroAnalysis.Id, 180, "Base sem variação", q)
        );
    }

    [Theory]
    [InlineData(Roles.Tecnico)]
    [InlineData(Roles.Engenheiro)]
    [InlineData(Roles.Leitor)]
    public async Task Limit_approval_permission_is_enforced_on_server(string role)
    {
        var op = await Operations();
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            op.ApproveAsync(1, 180, "Justificativa", User(role))
        );
        if (role != Roles.Engenheiro)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                op.PlanAsync(PlanInput(), User(role))
            );
        if (role == Roles.Leitor)
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                op.OpenAsync(1, "Teste", "operator-1", DateTimeOffset.UtcNow.AddDays(1), User(role))
            );
    }
}
