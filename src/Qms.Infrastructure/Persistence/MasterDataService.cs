using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qms.Application.Auditing;
using Qms.Application.MasterData;
using Qms.Domain.MasterData;

namespace Qms.Infrastructure.Persistence;

public sealed class MasterDataService(QmsDbContext db, IAuditService audit)
    : IMasterDataService,
        IInstrumentService
{
    public async Task<IReadOnlyList<T>> ListChildrenAsync<T>(
        int parentId,
        CancellationToken cancellationToken = default
    )
        where T : MasterDataEntity
    {
        var query = db.Set<T>().AsQueryable();
        var property = typeof(T).GetProperty(
            typeof(T).Name switch
            {
                "Product" => "AreaId",
                "Line" => "ProductId",
                "Station" => "LineId",
                "Tooling" => "StationId",
                "Characteristic" => "ToolingId",
                _ => "Id",
            }
        );
        if (property is not null && property.Name != "Id")
            query = query.Where(x => EF.Property<int>(x, property.Name) == parentId);
        return await query.OrderBy(x => x.Id).ToListAsync(cancellationToken);
    }

    public async Task<OperationResult<T>> CreateAsync<T>(
        T entity,
        string? actorId,
        CancellationToken cancellationToken = default
    )
        where T : MasterDataEntity
    {
        var error = Validate(entity);
        if (error is not null)
            return OperationResult<T>.Failure(error);
        db.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.Entry(entity).State = EntityState.Detached;
            return OperationResult<T>.Failure("Já existe um cadastro com esse nome ou código.");
        }
        await audit.RecordAsync(
            actorId,
            Table(typeof(T)),
            entity.Id.ToString(),
            $"{Table(typeof(T))}.create",
            after: entity,
            cancellationToken: cancellationToken
        );
        return OperationResult<T>.Success(entity);
    }

    public async Task<OperationResult<T>> UpdateAsync<T>(
        T entity,
        string? actorId,
        CancellationToken cancellationToken = default
    )
        where T : MasterDataEntity
    {
        var error = Validate(entity);
        if (error is not null)
            return OperationResult<T>.Failure(error);
        var child = typeof(T) switch
        {
            Type t when t == typeof(Area) => await db.Products.AnyAsync(
                x => x.AreaId == entity.Id && x.IsActive,
                cancellationToken
            ),
            Type t when t == typeof(Product) => await db.Lines.AnyAsync(
                x => x.ProductId == entity.Id && x.IsActive,
                cancellationToken
            ),
            Type t when t == typeof(Line) => await db.Stations.AnyAsync(
                x => x.LineId == entity.Id && x.IsActive,
                cancellationToken
            ),
            Type t when t == typeof(Station) => await db.Toolings.AnyAsync(
                x => x.StationId == entity.Id && x.IsActive,
                cancellationToken
            ),
            Type t when t == typeof(Tooling) => await db.Characteristics.AnyAsync(
                x => x.ToolingId == entity.Id && x.IsActive,
                cancellationToken
            ),
            _ => false,
        };
        if (!entity.IsActive && child)
            return OperationResult<T>.Failure(
                $"Não é possível desativar {Label(typeof(T))} com filhos ativos."
            );
        var before = await db.Set<T>()
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == entity.Id, cancellationToken);
        if (before is null)
            return OperationResult<T>.Failure("Registro não encontrado");
        db.Update(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return OperationResult<T>.Failure("Já existe um cadastro com esse nome ou código.");
        }
        await audit.RecordAsync(
            actorId,
            Table(typeof(T)),
            entity.Id.ToString(),
            $"{Table(typeof(T))}.update",
            before,
            entity,
            cancellationToken: cancellationToken
        );
        return OperationResult<T>.Success(entity);
    }

    public async Task<IReadOnlyList<string>> GetBreadcrumbAsync(
        string entity,
        int id,
        CancellationToken cancellationToken = default
    )
    {
        // Walk up the hierarchy one level at a time: each case adds its own name and
        // recurses into its parent, so the result is ordered from Area down to the item.
        var path = new List<string>();
        switch (entity)
        {
            case "areas":
                if (await db.Areas.FindAsync([id], cancellationToken) is { } area)
                    path.Add(area.Name);
                break;
            case "products":
                if (await db.Products.FindAsync([id], cancellationToken) is { } product)
                {
                    path.AddRange(
                        await GetBreadcrumbAsync("areas", product.AreaId, cancellationToken)
                    );
                    path.Add(product.Name);
                }
                break;
            case "lines":
                if (await db.Lines.FindAsync([id], cancellationToken) is { } line)
                {
                    path.AddRange(
                        await GetBreadcrumbAsync("products", line.ProductId, cancellationToken)
                    );
                    path.Add(line.Name);
                }
                break;
            case "stations":
                if (await db.Stations.FindAsync([id], cancellationToken) is { } station)
                {
                    path.AddRange(
                        await GetBreadcrumbAsync("lines", station.LineId, cancellationToken)
                    );
                    path.Add(station.Name);
                }
                break;
            case "toolings":
                if (await db.Toolings.FindAsync([id], cancellationToken) is { } tooling)
                {
                    path.AddRange(
                        await GetBreadcrumbAsync("stations", tooling.StationId, cancellationToken)
                    );
                    path.Add(tooling.Name);
                }
                break;
            case "characteristics":
                if (
                    await db.Characteristics.FindAsync([id], cancellationToken) is
                    { } characteristic
                )
                {
                    path.AddRange(
                        await GetBreadcrumbAsync(
                            "toolings",
                            characteristic.ToolingId,
                            cancellationToken
                        )
                    );
                    path.Add(characteristic.Name);
                }
                break;
        }
        return path;
    }

    public Task<IReadOnlyList<Instrument>> ListAsync(
        CancellationToken cancellationToken = default
    ) =>
        db
            .Instruments.OrderBy(x => x.Id)
            .ToListAsync(cancellationToken)
            .ContinueWith<IReadOnlyList<Instrument>>(x => x.Result, cancellationToken);

    public async Task<OperationResult<Instrument>> SaveAsync(
        Instrument instrument,
        string? actorId,
        CancellationToken cancellationToken = default
    )
    {
        if (
            string.IsNullOrWhiteSpace(instrument.Code)
            || string.IsNullOrWhiteSpace(instrument.Name)
            || string.IsNullOrWhiteSpace(instrument.Type)
        )
            return OperationResult<Instrument>.Failure("Informe código, nome e tipo.");
        if (instrument.Validate() is { } error)
            return OperationResult<Instrument>.Failure(error);
        if (
            await db.Instruments.AnyAsync(
                x => x.Code == instrument.Code && x.Id != instrument.Id,
                cancellationToken
            )
        )
            return OperationResult<Instrument>.Failure("Código de instrumento já cadastrado.");
        var before =
            instrument.Id == 0
                ? null
                : await db
                    .Instruments.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == instrument.Id, cancellationToken);
        if (instrument.Id != 0 && before is null)
            return OperationResult<Instrument>.Failure("Instrumento não encontrado.");
        if (before is null)
            db.Instruments.Add(instrument);
        else
            db.Instruments.Update(instrument);
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(
            actorId,
            "instruments",
            instrument.Id.ToString(),
            before is null ? "instrument.create" : "instrument.update",
            before,
            instrument,
            cancellationToken: cancellationToken
        );
        return OperationResult<Instrument>.Success(instrument);
    }

    private static string? Validate<T>(T entity)
        where T : MasterDataEntity => entity is Characteristic c ? c.Validate() : null;

    private static string Table(Type type) =>
        type.Name.ToLowerInvariant() switch
        {
            "characteristic" => "characteristics",
            "tooling" => "toolings",
            _ => type.Name.ToLowerInvariant() + "s",
        };

    private static string Label(Type type) =>
        type == typeof(Area) ? "área"
        : type == typeof(Product) ? "produto"
        : type == typeof(Line) ? "linha"
        : type == typeof(Station) ? "posto"
        : type == typeof(Tooling) ? "tooling"
        : "característica";
}

public sealed class SettingsService(QmsDbContext db, IAuditService audit)
    : Qms.Application.Settings.ISettingsService
{
    private static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.SnakeCaseLower,
    };

    public async Task<Qms.Application.Settings.QualitySettings> GetActiveAsync(
        CancellationToken cancellationToken = default
    )
    {
        var latest = await db
            .SettingsVersions.Include(x => x.Values)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
        if (latest is null)
            return Qms.Application.Settings.QualitySettings.Defaults;
        return Read(latest.Values);
    }

    public async Task<(bool Succeeded, int? Version, string? Error)> SaveAsync(
        Qms.Application.Settings.QualitySettings settings,
        string? comment,
        string? actorId,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(comment))
            return (false, null, "Informe um comentário para salvar a versão.");
        if (settings.Validate() is { } error)
            return (false, null, error);
        var previous = await db
            .SettingsVersions.Include(x => x.Values)
            .OrderByDescending(x => x.Version)
            .FirstOrDefaultAsync(cancellationToken);
        var old = previous is null
            ? Qms.Application.Settings.QualitySettings.Defaults
            : Read(previous.Values);
        var version = new Qms.Domain.Settings.SettingsVersion
        {
            Version = (previous?.Version ?? 0) + 1,
            CreatedBy = actorId,
            Comment = comment.Trim(),
        };
        foreach (var pair in Flatten(settings))
            version.Values.Add(
                new Qms.Domain.Settings.SettingValue
                {
                    Key = pair.Key,
                    ValueJson = System.Text.Json.JsonSerializer.Serialize(pair.Value, JsonOptions),
                }
            );
        db.SettingsVersions.Add(version);
        await db.SaveChangesAsync(cancellationToken);
        var before = Flatten(old);
        var after = Flatten(settings);
        await audit.RecordAsync(
            actorId,
            "settings",
            version.Version.ToString(),
            "settings.update",
            before
                .Where(x => !Equals(x.Value, after[x.Key]))
                .ToDictionary(x => x.Key, x => x.Value),
            after
                .Where(x => !Equals(x.Value, before[x.Key]))
                .ToDictionary(x => x.Key, x => x.Value),
            cancellationToken: cancellationToken
        );
        return (true, version.Version, null);
    }

    public async Task<IReadOnlyList<Qms.Application.Settings.SettingsHistory>> GetHistoryAsync(
        CancellationToken cancellationToken = default
    )
    {
        var rows = await db
            .SettingsVersions.Include(x => x.Values)
            .OrderByDescending(x => x.Version)
            .ToListAsync(cancellationToken);
        var result = new List<Qms.Application.Settings.SettingsHistory>();
        for (var i = 0; i < rows.Count; i++)
        {
            var diff = new Dictionary<string, string>();
            if (i + 1 < rows.Count)
            {
                var prior = Flatten(Read(rows[i + 1].Values));
                var now = Flatten(Read(rows[i].Values));
                foreach (var key in now.Keys.Where(k => !Equals(now[k], prior[k])))
                    diff[
                        Qms.Application.Settings.SettingsPresentation.Labels.GetValueOrDefault(
                            key,
                            key
                        )
                    ] =
                        $"{Qms.Application.Settings.SettingsPresentation.Describe(key, prior[key])} → {Qms.Application.Settings.SettingsPresentation.Describe(key, now[key])}";
            }
            result.Add(
                new(rows[i].Version, rows[i].CreatedAt, rows[i].CreatedBy, rows[i].Comment, diff)
            );
        }
        return result;
    }

    private static Qms.Application.Settings.QualitySettings Read(
        IEnumerable<Qms.Domain.Settings.SettingValue> items
    )
    {
        var values = items.ToDictionary(x => x.Key, x => x.ValueJson);
        T Get<T>(string key, T fallback) =>
            values.TryGetValue(key, out var json)
                ? System.Text.Json.JsonSerializer.Deserialize<T>(json, JsonOptions)!
                : fallback;
        var d = Qms.Application.Settings.QualitySettings.Defaults;
        return new(
            Get("capability.cp", d.Cp),
            Get("capability.cpk", d.Cpk),
            Get("capability.pp", d.Pp),
            Get("capability.ppk", d.Ppk),
            Get("chart.outside_3sigma", d.Outside3Sigma),
            Get("chart.same_side", d.SameSide),
            Get("chart.trend", d.Trend),
            Get("chart.spec_proximity", d.SpecProximity),
            Get("frequency.control_limits_days", d.ControlLimitsDays)
        );
    }

    private static Dictionary<string, object?> Flatten(
        Qms.Application.Settings.QualitySettings s
    ) =>
        new()
        {
            ["capability.cp"] = s.Cp,
            ["capability.cpk"] = s.Cpk,
            ["capability.pp"] = s.Pp,
            ["capability.ppk"] = s.Ppk,
            ["chart.outside_3sigma"] = s.Outside3Sigma,
            ["chart.same_side"] = s.SameSide,
            ["chart.trend"] = s.Trend,
            ["chart.spec_proximity"] = s.SpecProximity,
            ["frequency.control_limits_days"] = s.ControlLimitsDays,
        };
}
