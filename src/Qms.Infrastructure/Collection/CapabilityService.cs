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

public sealed class CapabilityService(IServiceScopeFactory scopes) : ICapabilityService
{
    private static string Authorize(ClaimsPrincipal user, string permission)
    {
        if (user.Identity?.IsAuthenticated != true || !Roles.All.Any(r => user.IsInRole(r) && Permissions.Allows(r, permission)))
            throw new UnauthorizedAccessException("Seu perfil não permite esta operação.");
        return user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException();
    }
    public async Task<CapabilityStudy> RunAsync(int datasetId, ClaimsPrincipal user)
    {
        var actor = Authorize(user, Permissions.StudiesRun);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var set = await db.CepDatasets.AsNoTracking().Include(x => x.Samples).SingleOrDefaultAsync(x => x.Id == datasetId)
            ?? throw new ArgumentException("Conjunto não encontrado.");
        if (set.StudyKind != "CAPABILITY" || !set.CompletedAt.HasValue || set.CompleteSamples != 33)
            throw new ArgumentException("Conclua um conjunto Cp/Cpk de 33 amostras × 3 leituras antes de analisar.");
        var version = await db.SettingsVersions.Select(x => (int?)x.Version).MaxAsync() ?? 0;
        var existing = await db.CapabilityStudies.AsNoTracking().SingleOrDefaultAsync(x => x.DatasetId == set.Id && x.DatasetVersion == set.Version && x.SettingsVersion == version && x.Method == CapabilityCalculator.Method);
        if (existing is not null) return existing;
        var settings = await scope.ServiceProvider.GetRequiredService<ISettingsService>().GetActiveAsync();
        var result = CapabilityCalculator.Calculate(set.Samples.Select(x => new CepReading(x.Position, x.Value1!.Value, x.Value2!.Value, x.Value3!.Value)).ToArray(), set.Lsl, set.Usl);
        var study = new CapabilityStudy
        {
            DatasetId = set.Id,
            DatasetVersion = set.Version,
            SettingsVersion = version,
            Method = result.Method,
            CreatedAt = DateTimeOffset.UtcNow,
            CreatedBy = actor,
            InputJson = JsonSerializer.Serialize(new { Dataset = set, Settings = settings }),
            ResultJson = JsonSerializer.Serialize(new CapabilityStudyResult(result, settings.Cp.GoodFrom, settings.Cpk.GoodFrom,
                Classifier.Classify("cp", result.Cp, settings).ToString()!, Classifier.Classify("cpk", result.Cpk, settings).ToString()!))
        };
        db.CapabilityStudies.Add(study);
        await db.SaveChangesAsync();
        await scope.ServiceProvider.GetRequiredService<IAuditService>().RecordAsync(actor, "capability_study", study.Id.ToString(), "capability.analysis.created", after: study, origin: "web");
        await tx.CommitAsync();
        return study;
    }
    public async Task<IReadOnlyList<CapabilityStudy>> ListAsync(ClaimsPrincipal user)
    {
        Authorize(user, Permissions.View);
        using var scope = scopes.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<QmsDbContext>().CapabilityStudies.AsNoTracking().OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id).ToListAsync();
    }
}
