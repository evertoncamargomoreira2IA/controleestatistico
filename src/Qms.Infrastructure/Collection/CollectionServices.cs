using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Qms.Application.Auditing;
using Qms.Application.Collection;
using Qms.Domain.Collection;
using Qms.Domain.MasterData;
using Qms.Infrastructure.Persistence;

namespace Qms.Infrastructure.Collection;

public sealed class CollectionService(QmsDbContext db, IAuditService audit) : ICollectionService
{
    public async Task<IReadOnlyList<Subgroup>> ListRecentAsync(
        int characteristicId,
        int take = 20,
        CancellationToken cancellationToken = default
    ) =>
        await db
            .Subgroups.Include(x => x.Measurements)
            .Where(x => x.CharacteristicId == characteristicId)
            .OrderByDescending(x => x.CollectedAt)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);

    public async Task<Subgroup> SaveAsync(
        CollectionInput input,
        string actorId,
        CancellationToken cancellationToken = default
    )
    {
        var characteristic =
            await db.Characteristics.FindAsync([input.CharacteristicId], cancellationToken)
            ?? throw new ArgumentException("Característica não encontrada.");
        var instrument = input.InstrumentId is null
            ? null
            : await db.Instruments.FindAsync([input.InstrumentId], cancellationToken);
        var result = SubgroupValidator.Validate(
            characteristic,
            input.Values,
            instrument,
            input.CollectedAt,
            DateTimeOffset.UtcNow
        );
        if (result.Errors.Count != 0)
            throw new ArgumentException(string.Join(" ", result.Errors));
        if (input.Shift is not null && input.Shift is not ("T1" or "T2" or "T3"))
            throw new ArgumentException("Turno inválido; informe T1, T2 ou T3.");
        var item = new Subgroup
        {
            CharacteristicId = characteristic.Id,
            CharacteristicRevision = characteristic.Revision,
            InstrumentId = instrument?.Id,
            OperatorId = actorId,
            CollectedAt = input.CollectedAt.ToUniversalTime(),
            Shift = input.Shift,
            Lot = input.Lot,
            Note = input.Note,
            Source = "online",
            Measurements = result
                .Parsed.Select(
                    (value, index) => new Measurement { Position = index + 1, Value = value }
                )
                .ToList(),
        };
        db.Subgroups.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(
            actorId,
            "subgroup",
            item.Id.ToString(),
            "subgroup.create",
            after: item,
            cancellationToken: cancellationToken
        );
        return item;
    }

    public async Task<bool> ExcludeAsync(
        int subgroupId,
        string reason,
        string actor,
        CancellationToken cancellationToken = default
    )
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Informe o motivo da exclusão.");
        var item = await db
            .Subgroups.Include(x => x.Measurements)
            .FirstOrDefaultAsync(x => x.Id == subgroupId, cancellationToken);
        if (item is null)
            return false;
        var before = new { item.IsExcluded, item.ExclusionReason };
        item.IsExcluded = true;
        item.ExclusionReason = reason.Trim();
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(
            actor,
            "subgroup",
            item.Id.ToString(),
            "subgroup.exclude",
            before,
            new { item.IsExcluded, item.ExclusionReason },
            cancellationToken: cancellationToken
        );
        return true;
    }
}

public sealed class TemplateService : ITemplateService
{
    private static string[] Headers(Characteristic c) =>
        [
            "tooling_code",
            "characteristic_name",
            "characteristic_id",
            "data_hora",
            "instrumento_codigo",
            "turno",
            "lote",
            "observacao",
            .. Enumerable.Range(1, c.SubgroupSize).Select(x => $"v{x}"),
        ];

    public byte[] BuildCsv(Characteristic c)
    {
        var rows = new List<string>
        {
            string.Join(';', Headers(c)),
            string.Join(
                ';',
                new[]
                {
                    "",
                    c.Name,
                    c.Id.ToString(CultureInfo.InvariantCulture),
                    "15/10/2026 12:00",
                    "CAL-001",
                    "T1",
                    "LOTE-EXEMPLO",
                    "Exemplo",
                }.Concat(Enumerable.Repeat("10", c.SubgroupSize))
            ),
        };
        return Encoding
            .UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(string.Join("\r\n", rows) + "\r\n"))
            .ToArray();
    }

    public byte[] BuildXlsx(Characteristic c)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Coleta");
        var headers = Headers(c);
        for (var i = 0; i < headers.Length; i++)
            sheet.Cell(1, i + 1).Value = headers[i];
        var sample = new[]
        {
            "",
            c.Name,
            c.Id.ToString(CultureInfo.InvariantCulture),
            "15/10/2026 12:00",
            "CAL-001",
            "T1",
            "LOTE-EXEMPLO",
            "Exemplo",
        }
            .Concat(Enumerable.Repeat("10", c.SubgroupSize))
            .ToArray();
        for (var i = 0; i < sample.Length; i++)
            sheet.Cell(2, i + 1).Value = sample[i];
        var instructions = workbook.AddWorksheet("Instrucoes");
        string[] lines =
        [
            "Instruções para preenchimento",
            "Use vírgula ou ponto como separador decimal.",
            "Informe um subgrupo por linha.",
            $"Preencha os {c.SubgroupSize} valores obrigatórios.",
            "Use um instrumento ativo e calibrado.",
            "Turno permitido: T1, T2 ou T3.",
            $"Unidade: {c.Unit ?? "—"}; LSL: {c.Lsl}; Nominal: {c.Nominal}; USL: {c.Usl}; Revisão: {c.Revision}",
        ];
        for (var i = 0; i < lines.Length; i++)
            instructions.Cell(i + 1, 1).Value = lines[i];
        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }
}
