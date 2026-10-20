using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qms.Application.Auditing;
using Qms.Application.Collection;
using Qms.Application.Settings;
using Qms.Domain.Collection;
using Qms.Domain.Security;
using Qms.Infrastructure.Persistence;
using Qms.Statistics;

namespace Qms.Infrastructure.Collection;

public sealed class CepAnalysisService(IServiceScopeFactory scopes) : ICepAnalysisService
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

    public async Task<CepAnalysis> RunAsync(
        int datasetId,
        ClaimsPrincipal user,
        int? approvedLimitId = null,
        bool operationalRules = false
    )
    {
        var actor = Authorize(user, Permissions.StudiesRun);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var dataset =
            await db
                .CepDatasets.AsNoTracking()
                .Include(x => x.Samples)
                .SingleOrDefaultAsync(x => x.Id == datasetId)
            ?? throw new ArgumentException("Conjunto não encontrado.");
        if (dataset.StudyKind != "CEP" || !dataset.CompletedAt.HasValue || dataset.CompleteSamples != 25)
            throw new ArgumentException(
                "Conclua as 25 amostras com três leituras antes de analisar."
            );
        CepLimitApproval? approval = null;
        if (approvedLimitId.HasValue)
        {
            operationalRules = true;
            approval =
                await db
                    .CepLimitApprovals.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == approvedLimitId.Value)
                ?? throw new ArgumentException("Limites aprovados não encontrados.");
            var source = await db
                .CepAnalyses.AsNoTracking()
                .SingleAsync(x => x.Id == approval.SourceAnalysisId);
            if (
                !await db.Characteristics.AnyAsync(x =>
                    x.Id == dataset.CharacteristicId
                    && x.Revision == dataset.CharacteristicRevision
                    && x.IsActive
                )
            )
                throw new ArgumentException(
                    "A característica mudou de revisão ou está inativa. Crie um conjunto com a revisão vigente."
                );
            if (
                approval.CharacteristicId != dataset.CharacteristicId
                || approval.CharacteristicRevision != dataset.CharacteristicRevision
                || approval.ExpiresAt < DateTimeOffset.UtcNow
                || await db.CepLimitRevocations.AnyAsync(x => x.ApprovalId == approval.Id)
                || source.DatasetId == dataset.Id
            )
                throw new ArgumentException(
                    "Limites expirados, revogados ou incompatíveis com a característica/revisão. A base não pode monitorar a si mesma."
                );
            if (
                dataset.Samples.Any(x =>
                    !x.CollectedAt.HasValue
                    || x.CollectedAt.Value < approval.ApprovedAt
                    || x.CollectedAt.Value > approval.ExpiresAt
                )
            )
                throw new ArgumentException(
                    "Os limites aprovados se aplicam somente a leituras coletadas durante sua vigência."
                );
        }
        var settings = await scope
            .ServiceProvider.GetRequiredService<ISettingsService>()
            .GetActiveAsync();
        var settingsVersion =
            await db.SettingsVersions.Select(x => (int?)x.Version).MaxAsync() ?? 0;
        var method = operationalRules
            ? $"xbar-r-25x3-v2-s{settingsVersion}-l{approvedLimitId?.ToString() ?? "0"}"
            : CepCalculator.Method;
        var existing = await db
            .CepAnalyses.AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.DatasetId == datasetId
                && x.DatasetVersion == dataset.Version
                && x.Method == method
            );
        if (existing is not null)
            return existing;
        var result = CepCalculator.Calculate(
            dataset
                .Samples.Select(x => new CepReading(
                    x.Position,
                    x.Value1!.Value,
                    x.Value2!.Value,
                    x.Value3!.Value
                ))
                .ToArray(),
            dataset.Nominal,
            dataset.Lsl,
            dataset.Usl
        );
        if (operationalRules)
        {
            var baseline = approval is null
                ? null
                : JsonSerializer.Deserialize<CepResult>(approval.SourceResultJson)!;
            var policy = new CepRulePolicy(
                settingsVersion,
                settings.Outside3Sigma,
                settings.SameSide.Enabled,
                settings.SameSide.N,
                settings.Trend.Enabled,
                settings.Trend.N,
                settings.SpecProximity.Enabled,
                settings.SpecProximity.Percent
            );
            result = CepCalculator.EvaluateOperational(
                dataset
                    .Samples.Select(x => new CepReading(
                        x.Position,
                        x.Value1!.Value,
                        x.Value2!.Value,
                        x.Value3!.Value
                    ))
                    .ToArray(),
                dataset.Nominal,
                dataset.Lsl,
                dataset.Usl,
                policy,
                baseline is null
                    ? null
                    : new CepControlLimits(
                        baseline.XLower,
                        baseline.XUpper,
                        baseline.RLower,
                        baseline.RUpper,
                        baseline.ControlCenter ?? baseline.Mean,
                        baseline.RangeCenter ?? baseline.AverageRange
                    ),
                approvedLimitId
            );
        }
        var analysis = new CepAnalysis
        {
            DatasetId = datasetId,
            DatasetVersion = dataset.Version,
            Method = result.Method,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actor,
            InputJson = JsonSerializer.Serialize(dataset),
            ResultJson = JsonSerializer.Serialize(result),
        };
        db.Add(analysis);
        await db.SaveChangesAsync();
        if (
            operationalRules
            && (result.Signals.Count > 0 || result.IndividualOutsideSpec > 0 || result.ZeroRange)
        )
        {
            var occurrence = new CepOccurrence
            {
                AnalysisId = analysis.Id,
                Description =
                    $"Análise #{analysis.Id}: {result.Signals.Count} sinais, {result.IndividualOutsideSpec} leituras fora da especificação; variação nula: {result.ZeroRange}.",
                ResponsibleId = actor,
                DueAt = DateTimeOffset.UtcNow.AddDays(1),
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
            };
            db.Add(occurrence);
            await db.SaveChangesAsync();
            await scope
                .ServiceProvider.GetRequiredService<IAuditService>()
                .RecordAsync(
                    actor,
                    "cep_occurrence",
                    occurrence.Id.ToString(),
                    "cep.occurrence.detected",
                    after: occurrence,
                    origin: "web"
                );
        }
        await scope
            .ServiceProvider.GetRequiredService<IAuditService>()
            .RecordAsync(
                actor,
                "cep_analysis",
                analysis.Id.ToString(),
                "cep.analysis.created",
                after: analysis,
                origin: "web"
            );
        await transaction.CommitAsync();
        return analysis;
    }

    public async Task<IReadOnlyList<CepAnalysis>> ListAsync(int? datasetId, ClaimsPrincipal user)
    {
        Authorize(user, Permissions.View);
        using var scope = scopes.CreateScope();
        var query = scope
            .ServiceProvider.GetRequiredService<QmsDbContext>()
            .CepAnalyses.AsNoTracking();
        if (datasetId.HasValue)
            query = query.Where(x => x.DatasetId == datasetId);
        return await query
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
    }

    public async Task<CepAnalysis> GetAsync(int id, ClaimsPrincipal user)
    {
        Authorize(user, Permissions.View);
        using var scope = scopes.CreateScope();
        return await scope
                .ServiceProvider.GetRequiredService<QmsDbContext>()
                .CepAnalyses.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new ArgumentException("Análise não encontrada.");
    }
}
