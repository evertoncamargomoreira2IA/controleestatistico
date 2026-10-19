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

    public async Task<CepAnalysis> RunAsync(int datasetId, ClaimsPrincipal user)
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
        if (!dataset.CompletedAt.HasValue || dataset.CompleteSamples != 25)
            throw new ArgumentException(
                "Conclua as 25 amostras com três leituras antes de analisar."
            );
        var existing = await db
            .CepAnalyses.AsNoTracking()
            .SingleOrDefaultAsync(x =>
                x.DatasetId == datasetId
                && x.DatasetVersion == dataset.Version
                && x.Method == CepCalculator.Method
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
