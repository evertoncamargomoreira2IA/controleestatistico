using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qms.Application.Auditing;
using Qms.Application.Collection;
using Qms.Domain.Collection;
using Qms.Domain.MasterData;
using Qms.Domain.Security;
using Qms.Infrastructure.Persistence;

namespace Qms.Infrastructure.Collection;

// A scope per operation keeps the context and transaction out of the Blazor circuit.
public sealed class CepDatasetService(IServiceScopeFactory scopes) : ICepDatasetService
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

    public async Task<IReadOnlyList<CepChoice>> ChoicesAsync(ClaimsPrincipal user)
    {
        Authorize(user, Permissions.View);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        return await (
            from c in db.Characteristics.AsNoTracking()
            join t in db.Toolings on c.ToolingId equals t.Id
            join s in db.Stations on t.StationId equals s.Id
            join l in db.Lines on s.LineId equals l.Id
            join p in db.Products on l.ProductId equals p.Id
            join a in db.Areas on p.AreaId equals a.Id
            where
                c.IsActive
                && t.IsActive
                && s.IsActive
                && l.IsActive
                && p.IsActive
                && a.IsActive
                && c.DataType == CharacteristicDataType.Continuous
                && c.SubgroupSize == 3
                && c.ChartType == ChartType.XbarR
            orderby a.Name, p.Name, l.Name, s.Name, t.Code, c.Name
            select new CepChoice(
                c,
                a.Name + " / " + p.Name + " / " + l.Name + " / " + s.Name + " / " + t.Code
            )
        ).ToListAsync();
    }

    public async Task<IReadOnlyList<Instrument>> InstrumentsAsync(ClaimsPrincipal user)
    {
        Authorize(user, Permissions.View);
        using var scope = scopes.CreateScope();
        return await scope
            .ServiceProvider.GetRequiredService<QmsDbContext>()
            .Instruments.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .ToListAsync();
    }

    public async Task<CepDataset> CreateAsync(
        int characteristicId,
        string title,
        ClaimsPrincipal user
    )
    {
        var actor = Authorize(user, Permissions.CollectionCreate);
        var choice =
            (await ChoicesAsync(user)).SingleOrDefault(x => x.Characteristic.Id == characteristicId)
            ?? throw new ArgumentException(
                "Selecione uma característica ativa X̄-R com três leituras por amostra."
            );
        title = ValidateTitle(title);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var c = choice.Characteristic;
        var now = DateTimeOffset.UtcNow;
        var item = new CepDataset
        {
            CharacteristicId = c.Id,
            CharacteristicRevision = c.Revision,
            CharacteristicName = c.Name,
            ProcessPath = choice.ProcessPath,
            Unit = c.Unit,
            Lsl = c.Lsl,
            Nominal = c.Nominal,
            Usl = c.Usl,
            Title = title,
            CreatedBy = actor,
            CreatedAt = now,
            UpdatedAt = now,
            Samples = Enumerable.Range(1, 25).Select(i => new CepSample { Position = i }).ToList(),
        };
        db.CepDatasets.Add(item);
        await db.SaveChangesAsync();
        await scope
            .ServiceProvider.GetRequiredService<IAuditService>()
            .RecordAsync(
                actor,
                "cep_dataset",
                item.Id.ToString(),
                "cep.create",
                after: item,
                origin: "web"
            );
        await tx.CommitAsync();
        return item;
    }

    public async Task<CepDataset> GetAsync(int id, ClaimsPrincipal user)
    {
        Authorize(user, Permissions.View);
        using var scope = scopes.CreateScope();
        var item =
            await scope
                .ServiceProvider.GetRequiredService<QmsDbContext>()
                .CepDatasets.AsNoTracking()
                .Include(x => x.Samples)
                .SingleOrDefaultAsync(x => x.Id == id)
            ?? throw new ArgumentException("Conjunto CEP não encontrado.");
        item.Samples = item.Samples.OrderBy(x => x.Position).ToList();
        return item;
    }

    public async Task<CepDataset> SaveAsync(CepSaveInput input, ClaimsPrincipal user)
    {
        var actor = Authorize(user, Permissions.CollectionCreate);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var item =
            await db.CepDatasets.Include(x => x.Samples).SingleOrDefaultAsync(x => x.Id == input.Id)
            ?? throw new ArgumentException("Conjunto CEP não encontrado.");
        if (item.CompletedAt.HasValue)
            throw new ArgumentException(
                "Conjunto concluído é somente leitura. Crie outro conjunto para uma nova coleta."
            );
        if (item.Version != input.Version)
            throw new ArgumentException(
                "O conjunto foi atualizado por outro usuário. Reabra antes de editar."
            );
        if (
            input.Rows.Count != 25
            || !input.Rows.Select(x => x.Position).Order().SequenceEqual(Enumerable.Range(1, 25))
        )
            throw new ArgumentException(
                "O conjunto deve ter exatamente 25 posições, sem repetição."
            );
        var title = ValidateTitle(input.Title);
        var before = System.Text.Json.JsonSerializer.SerializeToElement(item);
        var characteristic = await db.Characteristics.SingleAsync(x =>
            x.Id == item.CharacteristicId
        );
        if (
            !characteristic.IsActive
            || characteristic.Revision != item.CharacteristicRevision
            || characteristic.SubgroupSize != 3
            || characteristic.ChartType != ChartType.XbarR
            || characteristic.DataType != CharacteristicDataType.Continuous
        )
            throw new ArgumentException(
                "A característica foi alterada ou inativada. Preserve este histórico e crie outro conjunto com a revisão atual."
            );
        var sourceIds = input
            .Rows.Where(x => x.SourceSubgroupId.HasValue)
            .Select(x => x.SourceSubgroupId!.Value)
            .ToArray();
        if (sourceIds.Distinct().Count() != sourceIds.Length)
            throw new ArgumentException(
                "Uma amostra da base não pode ser repetida no mesmo conjunto."
            );
        var sources = await db
            .Subgroups.AsNoTracking()
            .Include(x => x.Measurements)
            .Where(x => sourceIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id);
        var instruments = await db.Instruments.AsNoTracking().ToDictionaryAsync(x => x.Id);
        var now = DateTimeOffset.UtcNow;
        foreach (var row in input.Rows.OrderBy(x => x.Position))
        {
            var target = item.Samples.Single(x => x.Position == row.Position);
            var values = new[]
            {
                Parse(row.Value1, row.Position),
                Parse(row.Value2, row.Position),
                Parse(row.Value3, row.Position),
            };
            if (values.Any(x => x.HasValue))
            {
                if (
                    row.CollectedAt is null
                    || row.InstrumentId is null
                    || !instruments.TryGetValue(row.InstrumentId.Value, out var instrument)
                )
                    throw new ArgumentException(
                        $"Amostra {row.Position}: informe instrumento e data da coleta."
                    );
                if (row.Shift is not ("T1" or "T2" or "T3"))
                    throw new ArgumentException($"Amostra {row.Position}: turno inválido.");
                if (row.Lot.Length > 80)
                    throw new ArgumentException("O lote aceita até 80 caracteres.");
                var validation = SubgroupValidator.Validate(
                    characteristic,
                    values.Where(x => x.HasValue).Select(x => (object?)x!.Value).ToArray(),
                    instrument,
                    row.CollectedAt.Value,
                    now
                );
                // Partial readings are allowed only in drafts. Other rules always apply.
                var errors = validation
                    .Errors.Where(x =>
                        !x.StartsWith("O subgrupo deve conter exatamente", StringComparison.Ordinal)
                    )
                    .ToArray();
                if (errors.Length != 0)
                    throw new ArgumentException(
                        $"Amostra {row.Position}: {string.Join(" ", errors)}"
                    );
            }
            if (row.SourceSubgroupId is int sourceId)
            {
                if (
                    !sources.TryGetValue(sourceId, out var source)
                    || source.IsExcluded
                    || source.CharacteristicId != item.CharacteristicId
                    || source.CharacteristicRevision != item.CharacteristicRevision
                    || source.Measurements.Count != 3
                )
                    throw new ArgumentException(
                        $"Amostra {row.Position}: origem excluída, incompatível ou de outra revisão."
                    );
                var original = source
                    .Measurements.OrderBy(x => x.Position)
                    .Select(x => (double?)x.Value)
                    .ToArray();
                if (
                    !values.SequenceEqual(original)
                    || row.InstrumentId != source.InstrumentId
                    || row.CollectedAt != source.CollectedAt
                    || row.Shift != source.Shift
                    || row.Lot != (source.Lot ?? "")
                )
                    throw new ArgumentException(
                        "Leituras e rastreabilidade da base não podem ser alteradas ao vincular."
                    );
                target.OperatorId = source.OperatorId;
                target.Source = "base";
                target.SourceSubgroupId = source.Id;
            }
            else
            {
                var changed =
                    target.Value1 != values[0]
                    || target.Value2 != values[1]
                    || target.Value3 != values[2]
                    || target.InstrumentId != row.InstrumentId
                    || target.CollectedAt != row.CollectedAt
                    || target.Shift != row.Shift
                    || target.Lot != row.Lot
                    || target.Source == "base";
                if (changed)
                    target.OperatorId = actor;
                target.Source = "frontend";
                target.SourceSubgroupId = null;
            }
            target.Value1 = values[0];
            target.Value2 = values[1];
            target.Value3 = values[2];
            target.InstrumentId = values.Any(x => x.HasValue) ? row.InstrumentId : null;
            target.CollectedAt = values.Any(x => x.HasValue)
                ? row.CollectedAt?.ToUniversalTime()
                : null;
            target.Shift = row.Shift;
            target.Lot = row.Lot.Trim();
            if (
                target.IsComplete
                && (!double.IsFinite(target.Mean!.Value) || !double.IsFinite(target.Range!.Value))
            )
                throw new ArgumentException(
                    $"Amostra {row.Position}: valores excedem a faixa numérica permitida."
                );
        }
        if (input.Complete && item.CompleteSamples != 25)
            throw new ArgumentException(
                "Para concluir, preencha as 25 amostras com três leituras cada (75 leituras)."
            );
        item.Title = title;
        item.UpdatedAt = now;
        item.Version++;
        if (input.Complete)
            item.CompletedAt = now;
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ArgumentException(
                "O conjunto foi atualizado por outro usuário. Reabra antes de editar."
            );
        }
        await scope
            .ServiceProvider.GetRequiredService<IAuditService>()
            .RecordAsync(
                actor,
                "cep_dataset",
                item.Id.ToString(),
                input.Complete ? "cep.complete" : "cep.save",
                before: before,
                after: item,
                origin: "web"
            );
        await tx.CommitAsync();
        item.Samples = item.Samples.OrderBy(x => x.Position).ToList();
        return item;
    }

    public async Task<IReadOnlyList<CepSourceRow>> SourcesAsync(
        int datasetId,
        DateTimeOffset from,
        DateTimeOffset until,
        string shift,
        string lot,
        ClaimsPrincipal user
    )
    {
        Authorize(user, Permissions.View);
        if (until <= from)
            throw new ArgumentException("O fim do período deve ser posterior ao início.");
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        var set = await db.CepDatasets.AsNoTracking().SingleAsync(x => x.Id == datasetId);
        var query = db
            .Subgroups.AsNoTracking()
            .Include(x => x.Measurements)
            .Where(x =>
                x.CharacteristicId == set.CharacteristicId
                && x.CharacteristicRevision == set.CharacteristicRevision
                && !x.IsExcluded
                && x.CollectedAt >= from
                && x.CollectedAt < until
                && x.Measurements.Count == 3
            );
        if (!string.IsNullOrWhiteSpace(shift))
            query = query.Where(x => x.Shift == shift);
        if (!string.IsNullOrWhiteSpace(lot))
            query = query.Where(x => x.Lot == lot);
        var data = await query
            .OrderBy(x => x.CollectedAt)
            .ThenBy(x => x.Id)
            .Take(501)
            .ToListAsync();
        if (data.Count > 500)
            throw new ArgumentException(
                "Mais de 500 amostras. Reduza o período ou filtre turno/lote."
            );
        var instrumentNames = await db.Instruments.ToDictionaryAsync(x => x.Id, x => x.Code);
        var userNames = await db.Users.ToDictionaryAsync(x => x.Id, x => x.FullName);
        return data.Select(x => new CepSourceRow(
                x,
                x.InstrumentId.HasValue
                    ? instrumentNames.GetValueOrDefault(x.InstrumentId.Value, "—")
                    : "—",
                userNames.GetValueOrDefault(x.OperatorId, x.OperatorId)
            ))
            .ToArray();
    }

    public async Task<IReadOnlyList<CepHistoryItem>> HistoryAsync(int id, ClaimsPrincipal user)
    {
        Authorize(user, Permissions.View);
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<QmsDbContext>();
        var logs = await db
            .AuditLogs.AsNoTracking()
            .Where(x => x.Entity == "cep_dataset" && x.EntityId == id.ToString())
            .OrderByDescending(x => x.OccurredAt)
            .ThenByDescending(x => x.Id)
            .ToListAsync();
        var names = await db.Users.ToDictionaryAsync(x => x.Id, x => x.FullName);
        return logs.Select(x => new CepHistoryItem(
                x.OccurredAt,
                names.GetValueOrDefault(x.ActorId ?? "", x.ActorId ?? "Sistema"),
                x.Action,
                x.BeforeJson,
                x.AfterJson
            ))
            .ToArray();
    }

    public async Task<CepDaily> DailyAsync(DateOnly day, ClaimsPrincipal user)
    {
        Authorize(user, Permissions.View);
        var start = LocalTime.ParseLocal(
            day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00"
        );
        var end = LocalTime.ParseLocal(
            day.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "T00:00"
        );
        using var scope = scopes.CreateScope();
        var sets = await scope
            .ServiceProvider.GetRequiredService<QmsDbContext>()
            .CepDatasets.AsNoTracking()
            .Include(x => x.Samples)
            .Where(x => x.CreatedAt >= start && x.CreatedAt < end)
            .OrderByDescending(x => x.UpdatedAt)
            .ToListAsync();
        return new(day, sets);
    }

    private static string ValidateTitle(string title) =>
        string.IsNullOrWhiteSpace(title) || title.Trim().Length > 120
            ? throw new ArgumentException("Informe um título de até 120 caracteres.")
            : title.Trim();

    private static double? Parse(string? text, int position)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;
        if (
            !double.TryParse(
                text.Trim().Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out var value
            ) || !double.IsFinite(value)
        )
            throw new ArgumentException(
                $"Amostra {position}: informe um número finito, usando vírgula ou ponto decimal."
            );
        return value;
    }
}
