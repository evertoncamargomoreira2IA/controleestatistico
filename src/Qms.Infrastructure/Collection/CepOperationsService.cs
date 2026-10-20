using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qms.Application.Auditing;
using Qms.Application.Collection;
using Qms.Domain.Collection;
using Qms.Domain.Security;
using Qms.Infrastructure.Persistence;
using Qms.Statistics;

namespace Qms.Infrastructure.Collection;

public sealed class CepOperationsService(IServiceScopeFactory scopes) : ICepOperationsService
{
    private static string Authorize(ClaimsPrincipal user, string permission)
    {
        if (
            user.Identity?.IsAuthenticated != true
            || !Roles.All.Any(r => user.IsInRole(r) && Permissions.Allows(r, permission))
        )
            throw new UnauthorizedAccessException("Seu perfil não permite esta operação.");
        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
    }

    private static string Text(string value, int max, string label) =>
        string.IsNullOrWhiteSpace(value) || value.Trim().Length > max
            ? throw new ArgumentException($"{label}: informe de 1 a {max} caracteres.")
            : value.Trim();

    private static string Day(string day) =>
        DateOnly.TryParseExact(
            day,
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var d
        )
            ? d.ToString("yyyy-MM-dd")
            : throw new ArgumentException("Informe uma data válida.");

    private static async Task Responsible(QmsDbContext db, string id)
    {
        if (!await db.Users.AnyAsync(x => x.Id == id && x.IsActive))
            throw new ArgumentException("Escolha um responsável ativo.");
    }

    private static async Task Audit(
        IServiceProvider provider,
        string actor,
        string entity,
        int id,
        string action,
        object? before,
        object? after
    ) =>
        await provider
            .GetRequiredService<IAuditService>()
            .RecordAsync(actor, entity, id.ToString(), action, before, after, origin: "web");

    public async Task<CepPlan> PlanAsync(CepPlanInput input, ClaimsPrincipal user)
    {
        var actor = Authorize(user, Permissions.CepPlanManage);
        var day = Day(input.Day);
        if (input.TargetSets is < 1 or > 100 || input.Shift is not ("T1" or "T2" or "T3"))
            throw new ArgumentException("Meta deve ser 1 a 100 conjuntos e turno T1, T2 ou T3.");
        if (LocalTime.ToLocal(input.DueAt).ToString("yyyy-MM-dd") != day)
            throw new ArgumentException("O prazo deve pertencer à data planejada em São Paulo.");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        await Responsible(db, input.ResponsibleId);
        var choice =
            (
                await scope
                    .ServiceProvider.GetRequiredService<ICepDatasetService>()
                    .ChoicesAsync(user)
            ).SingleOrDefault(x => x.Characteristic.Id == input.CharacteristicId)
            ?? throw new ArgumentException("Característica CEP ativa não encontrada.");
        var plan = new CepPlan
        {
            CharacteristicId = input.CharacteristicId,
            CharacteristicRevision = choice.Characteristic.Revision,
            CharacteristicName = choice.Characteristic.Name,
            ProcessPath = choice.ProcessPath,
            Day = day,
            Shift = input.Shift,
            Title = Text(input.Title, 120, "Título"),
            ResponsibleId = input.ResponsibleId,
            TargetSets = input.TargetSets,
            DueAt = input.DueAt.ToUniversalTime(),
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actor,
        };
        db.Add(plan);
        await db.SaveChangesAsync();
        await Audit(
            scope.ServiceProvider,
            actor,
            "cep_plan",
            plan.Id,
            "cep.plan.created",
            null,
            plan
        );
        await tx.CommitAsync();
        return plan;
    }

    public async Task LinkAsync(int planId, int datasetId, int version, ClaimsPrincipal user)
    {
        var actor = Authorize(user, Permissions.CollectionCreate);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var plan =
            await db.CepPlans.SingleOrDefaultAsync(x => x.Id == planId)
            ?? throw new ArgumentException("Programação não encontrada.");
        if (plan.Cancelled || plan.Version != version)
            throw new ArgumentException("Programação cancelada ou alterada. Recarregue a página.");
        var set =
            await db
                .CepDatasets.Include(x => x.Samples)
                .SingleOrDefaultAsync(x => x.Id == datasetId)
            ?? throw new ArgumentException("Conjunto não encontrado.");
        if (
            set.StudyKind != "CEP"
            || set.CharacteristicId != plan.CharacteristicId
            || set.CharacteristicRevision != plan.CharacteristicRevision
            || set.Samples.Any(x => x.IsComplete && x.Shift != plan.Shift)
        )
            throw new ArgumentException("Conjunto de outra característica, revisão ou turno.");
        if (await db.CepPlanDatasets.AnyAsync(x => x.DatasetId == datasetId))
            throw new ArgumentException("O conjunto já está vinculado a uma programação.");
        if (await db.CepPlanDatasets.CountAsync(x => x.PlanId == planId) >= plan.TargetSets)
            throw new ArgumentException("A meta já possui todos os conjuntos vinculados.");
        var before = JsonSerializer.Serialize(plan);
        plan.Version++;
        var link = new CepPlanDataset
        {
            PlanId = planId,
            DatasetId = datasetId,
            LinkedAt = DateTimeOffset.UtcNow,
            LinkedBy = actor,
        };
        db.Add(link);
        await db.SaveChangesAsync();
        await Audit(
            scope.ServiceProvider,
            actor,
            "cep_plan",
            plan.Id,
            "cep.plan.linked",
            JsonSerializer.Deserialize<object>(before),
            new { Plan = plan, Link = link }
        );
        await tx.CommitAsync();
    }

    public async Task CancelAsync(int planId, int version, string reason, ClaimsPrincipal user)
    {
        var actor = Authorize(user, Permissions.CepPlanManage);
        reason = Text(reason, 1000, "Motivo");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var plan =
            await db.CepPlans.SingleOrDefaultAsync(x => x.Id == planId)
            ?? throw new ArgumentException("Programação não encontrada.");
        if (plan.Version != version || plan.Cancelled)
            throw new ArgumentException("Programação alterada ou cancelada. Recarregue.");
        var before = JsonSerializer.Serialize(plan);
        plan.Cancelled = true;
        plan.Version++;
        await db.SaveChangesAsync();
        await Audit(
            scope.ServiceProvider,
            actor,
            "cep_plan",
            plan.Id,
            "cep.plan.cancelled",
            JsonSerializer.Deserialize<object>(before),
            new { Plan = plan, Reason = reason }
        );
        await tx.CommitAsync();
    }

    public async Task<CepLimitApproval> ApproveAsync(
        int analysisId,
        int validityDays,
        string reason,
        ClaimsPrincipal user
    )
    {
        var actor = Authorize(user, Permissions.CepLimitsApprove);
        reason = Text(reason, 1000, "Justificativa técnica");
        if (validityDays is < 1 or > 3650)
            throw new ArgumentException("Validade deve ser 1 a 3650 dias.");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var analysis =
            await db.CepAnalyses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == analysisId)
            ?? throw new ArgumentException("Análise não encontrada.");
        var source = JsonSerializer.Deserialize<CepDataset>(analysis.InputJson)!;
        var result = JsonSerializer.Deserialize<CepResult>(analysis.ResultJson)!;
        if (result.Signals.Count > 0 || result.ZeroRange || result.IndividualOutsideSpec > 0)
            throw new ArgumentException(
                "A base deve ter variação positiva e nenhuma ocorrência estatística ou de especificação."
            );
        if (result.ApprovedLimitId.HasValue)
            throw new ArgumentException(
                "Use uma análise com limites estimados para estabelecer uma nova base."
            );
        if (
            !await db.Characteristics.AnyAsync(x =>
                x.Id == source.CharacteristicId
                && x.Revision == source.CharacteristicRevision
                && x.IsActive
            )
        )
            throw new ArgumentException("Revisão da característica alterada ou inativa.");
        if (
            await db.CepOccurrences.AnyAsync(x =>
                x.AnalysisId == analysisId && x.Status != "Encerrada"
            )
        )
            throw new ArgumentException("Encerre a investigação da base antes de aprovar.");
        if (await db.CepLimitApprovals.AnyAsync(x => x.SourceAnalysisId == analysisId))
            throw new ArgumentException(
                "Esta análise já originou uma aprovação. Use uma nova base para uma nova versão."
            );
        var approval = new CepLimitApproval
        {
            SourceAnalysisId = analysisId,
            CharacteristicId = source.CharacteristicId,
            CharacteristicRevision = source.CharacteristicRevision,
            ResponsibleId = actor,
            Reason = reason,
            ApprovedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(validityDays),
            SourceResultJson = analysis.ResultJson,
        };
        db.Add(approval);
        await db.SaveChangesAsync();
        await Audit(
            scope.ServiceProvider,
            actor,
            "cep_limit",
            approval.Id,
            "cep.limit.approved",
            null,
            approval
        );
        await tx.CommitAsync();
        return approval;
    }

    public async Task RevokeAsync(int approvalId, string reason, ClaimsPrincipal user)
    {
        var actor = Authorize(user, Permissions.CepLimitsApprove);
        reason = Text(reason, 1000, "Motivo da revogação");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        if (!await db.CepLimitApprovals.AnyAsync(x => x.Id == approvalId))
            throw new ArgumentException("Aprovação não encontrada.");
        if (await db.CepLimitRevocations.AnyAsync(x => x.ApprovalId == approvalId))
            throw new ArgumentException("Aprovação já revogada.");
        var item = new CepLimitRevocation
        {
            ApprovalId = approvalId,
            ActorId = actor,
            At = DateTimeOffset.UtcNow,
            Reason = reason,
        };
        db.Add(item);
        await db.SaveChangesAsync();
        await Audit(
            scope.ServiceProvider,
            actor,
            "cep_limit",
            approvalId,
            "cep.limit.revoked",
            null,
            item
        );
        await tx.CommitAsync();
    }

    public async Task<CepOccurrence> OpenAsync(
        int analysisId,
        string description,
        string responsibleId,
        DateTimeOffset dueAt,
        ClaimsPrincipal user
    )
    {
        var actor = Authorize(user, Permissions.CepActionsManage);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        await Responsible(db, responsibleId);
        if (!await db.CepAnalyses.AnyAsync(x => x.Id == analysisId))
            throw new ArgumentException("Análise não encontrada.");
        if (await db.CepOccurrences.AnyAsync(x => x.AnalysisId == analysisId))
            throw new ArgumentException(
                "A análise já possui ocorrência; consulte e atualize a investigação existente."
            );
        if (dueAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException("Defina um prazo futuro para a investigação.");
        var item = new CepOccurrence
        {
            AnalysisId = analysisId,
            Description = Text(description, 2000, "Descrição"),
            ResponsibleId = responsibleId,
            DueAt = dueAt.ToUniversalTime(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        db.Add(item);
        await db.SaveChangesAsync();
        await Audit(
            scope.ServiceProvider,
            actor,
            "cep_occurrence",
            item.Id,
            "cep.occurrence.opened",
            null,
            item
        );
        await tx.CommitAsync();
        return item;
    }

    public async Task UpdateAsync(CepOccurrenceInput input, ClaimsPrincipal user)
    {
        var actor = Authorize(user, Permissions.CepActionsManage);
        if (input.Status is not ("Aberta" or "Em investigação" or "Encerrada"))
            throw new ArgumentException("Situação inválida.");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var item =
            await db.CepOccurrences.SingleOrDefaultAsync(x => x.Id == input.Id)
            ?? throw new ArgumentException("Ocorrência não encontrada.");
        if (item.Version != input.Version)
            throw new ArgumentException("Ocorrência alterada por outra pessoa. Recarregue.");
        await Responsible(db, input.ResponsibleId);
        if (input.Status == "Encerrada")
        {
            Text(input.Cause, 2000, "Causa");
            Text(input.Action, 2000, "Ação");
            Text(input.Verification, 2000, "Verificação de eficácia");
        }
        if (
            input.Cause.Length > 2000
            || input.Action.Length > 2000
            || input.Verification.Length > 2000
        )
            throw new ArgumentException("Campos da investigação limitados a 2000 caracteres.");
        if (input.Status != "Encerrada" && input.DueAt <= DateTimeOffset.UtcNow)
            throw new ArgumentException(
                "Defina um prazo futuro ao atualizar uma ocorrência aberta."
            );
        var before = JsonSerializer.Serialize(item);
        item.ResponsibleId = input.ResponsibleId;
        item.DueAt = input.DueAt.ToUniversalTime();
        item.Status = input.Status;
        item.Cause = input.Cause.Trim();
        item.Action = input.Action.Trim();
        item.Verification = input.Verification.Trim();
        item.ClosedAt = input.Status == "Encerrada" ? DateTimeOffset.UtcNow : null;
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version++;
        await db.SaveChangesAsync();
        await Audit(
            scope.ServiceProvider,
            actor,
            "cep_occurrence",
            item.Id,
            "cep.occurrence.updated",
            JsonSerializer.Deserialize<object>(before),
            item
        );
        await tx.CommitAsync();
    }

    public async Task<CepOperationsBoard> BoardAsync(string day, ClaimsPrincipal user)
    {
        Authorize(user, Permissions.View);
        day = Day(day);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        var plans = await db
            .CepPlans.AsNoTracking()
            .Where(x => x.Day == day)
            .OrderBy(x => x.DueAt)
            .ToListAsync();
        var planIds = plans.Select(x => x.Id).ToArray();
        var links = await db
            .CepPlanDatasets.AsNoTracking()
            .Where(x => planIds.Contains(x.PlanId))
            .ToListAsync();
        var setIds = links.Select(x => x.DatasetId).ToArray();
        var sets = await db
            .CepDatasets.AsNoTracking()
            .Include(x => x.Samples)
            .Where(x => setIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        var analyses = await db
            .CepAnalyses.AsNoTracking()
            .Where(x => setIds.Contains(x.DatasetId))
            .ToListAsync();
        var occurrences = await db
            .CepOccurrences.AsNoTracking()
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync();
        var progress = plans
            .Select(p =>
            {
                var ids = links.Where(x => x.PlanId == p.Id).Select(x => x.DatasetId).ToArray();
                var valid = ids.Where(id =>
                        sets[id].CompletedAt.HasValue
                        && sets[id].CharacteristicRevision == p.CharacteristicRevision
                        && sets[id].Samples.All(s => s.Shift == p.Shift)
                    )
                    .ToArray();
                var analyzed = valid.Count(id => analyses.Any(a => a.DatasetId == id));
                var analysisIds = analyses
                    .Where(a => ids.Contains(a.DatasetId))
                    .Select(a => a.Id)
                    .ToArray();
                var open = occurrences.Count(o =>
                    analysisIds.Contains(o.AnalysisId) && o.Status != "Encerrada"
                );
                var done = valid.Length >= p.TargetSets && analyzed >= p.TargetSets && open == 0;
                var status =
                    p.Cancelled ? "Cancelada"
                    : done ? "Concluída"
                    : p.DueAt < DateTimeOffset.UtcNow ? "Atrasada"
                    : open > 0 || valid.Length >= p.TargetSets ? "Em análise"
                    : ids.Length > 0 ? "Em coleta"
                    : "Planejada";
                return new CepPlanProgress(
                    p,
                    ids.Length,
                    valid.Length,
                    analyzed,
                    open,
                    status,
                    ids
                );
            })
            .ToArray();
        return new(
            progress,
            occurrences,
            await db.CepLimitApprovals.AsNoTracking().OrderByDescending(x => x.Id).ToListAsync(),
            await db.CepLimitRevocations.Select(x => x.ApprovalId).ToListAsync(),
            await db
                .Users.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.FullName)
                .Select(x => new CepOperator(x.Id, x.FullName))
                .ToListAsync()
        );
    }

    public async Task<IReadOnlyList<CepOperationEvent>> HistoryAsync(
        string entity,
        int id,
        ClaimsPrincipal user
    )
    {
        Authorize(user, Permissions.View);
        if (entity is not ("cep_plan" or "cep_limit" or "cep_occurrence"))
            throw new ArgumentException("Histórico inválido.");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        var logs = await db
            .AuditLogs.AsNoTracking()
            .Where(x => x.Entity == entity && x.EntityId == id.ToString())
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        var names = await db.Users.ToDictionaryAsync(x => x.Id, x => x.FullName);
        return logs.Select(x => new CepOperationEvent(
                x.OccurredAt,
                names.GetValueOrDefault(x.ActorId ?? "", x.ActorId ?? "Sistema"),
                x.Action,
                x.BeforeJson,
                x.AfterJson
            ))
            .ToArray();
    }
}
