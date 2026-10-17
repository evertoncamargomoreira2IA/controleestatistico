using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Qms.Application.Auditing;
using Qms.Application.Collection;
using Qms.Domain.Collection;
using Qms.Infrastructure.Persistence;

namespace Qms.Infrastructure.Collection;

/// <summary>
/// Reads collection files (CSV or the XLSX template), validates every row on the server and
/// imports only the valid ones. Mirrors app/services/import_service.py.
/// </summary>
public sealed class ImportService(QmsDbContext db, IAuditService audit) : IImportService
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxRows = 5000;

    private static readonly string[] RequiredHeaders =
    [
        "tooling_code",
        "characteristic_name",
        "characteristic_id",
        "data_hora",
        "instrumento_codigo",
        "turno",
        "lote",
        "observacao",
    ];

    public async Task<IReadOnlyList<Dictionary<string, object?>>> ParseAsync(
        string fileName,
        Stream stream,
        CancellationToken cancellationToken = default
    )
    {
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length > MaxFileBytes)
            throw new ArgumentException("O arquivo excede o limite de 5 MB.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var table = extension switch
        {
            ".xlsx" => ReadXlsx(buffer.ToArray()),
            ".csv" => ReadCsv(buffer.ToArray()),
            _ => throw new ArgumentException("Envie um arquivo .xlsx ou .csv."),
        };
        return ToRows(table);
    }

    public async Task<IReadOnlyList<CollectionRow>> PreviewAsync(
        IReadOnlyList<Dictionary<string, object?>> rows,
        CancellationToken cancellationToken = default
    )
    {
        var characteristicIds = rows.Select(row => ParseInt(Text(row, "characteristic_id")))
            .OfType<int>()
            .Distinct()
            .ToArray();
        var characteristics = await db
            .Characteristics.Where(x => characteristicIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var toolingIds = characteristics.Values.Select(x => x.ToolingId).Distinct().ToArray();
        var toolings = await db
            .Toolings.Where(x => toolingIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var instrumentCodes = rows.Select(row => Text(row, "instrumento_codigo"))
            .Where(code => code.Length > 0)
            .Distinct()
            .ToArray();
        var instruments = await db
            .Instruments.Where(x => instrumentCodes.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code, cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var result = new List<CollectionRow>(rows.Count);
        foreach (var row in rows)
        {
            var errors = new List<string>();
            var line = row.TryGetValue("linha", out var lineValue) ? Convert.ToInt32(lineValue) : 0;
            var rawValues = row.TryGetValue("values", out var valuesObject)
                ? ((IEnumerable<string>)valuesObject!).ToArray()
                : [];

            Domain.MasterData.Characteristic? characteristic = null;
            var characteristicId = ParseInt(Text(row, "characteristic_id"));
            if (
                characteristicId is null
                || !characteristics.TryGetValue(characteristicId.Value, out characteristic)
            )
            {
                errors.Add("Característica não encontrada (characteristic_id).");
            }
            else
            {
                var toolingCode = Text(row, "tooling_code");
                var tooling = toolings.GetValueOrDefault(characteristic.ToolingId);
                if (
                    toolingCode.Length > 0
                    && !string.Equals(
                        tooling?.Code,
                        toolingCode,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                    errors.Add("O tooling_code não corresponde à característica informada.");
                var name = Text(row, "characteristic_name");
                if (
                    name.Length > 0
                    && !string.Equals(characteristic.Name, name, StringComparison.OrdinalIgnoreCase)
                )
                    errors.Add("O characteristic_name não corresponde à característica informada.");
            }

            var instrumentCode = Text(row, "instrumento_codigo");
            var instrument =
                instrumentCode.Length > 0 ? instruments.GetValueOrDefault(instrumentCode) : null;
            if (instrumentCode.Length > 0 && instrument is null)
                errors.Add($"Instrumento \"{instrumentCode}\" não cadastrado.");

            DateTimeOffset? collectedAt = null;
            try
            {
                collectedAt = LocalTime.ParseLocal(Text(row, "data_hora"));
            }
            catch (ArgumentException)
            {
                errors.Add("Data e hora inválidas; use dd/mm/aaaa hh:mm.");
            }

            var shift = Text(row, "turno");
            if (shift is not ("T1" or "T2" or "T3"))
                errors.Add("Turno inválido; informe T1, T2 ou T3.");

            var values = Array.Empty<double>();
            if (characteristic is not null)
            {
                var validation = SubgroupValidator.Validate(
                    characteristic,
                    rawValues,
                    instrument,
                    collectedAt ?? now,
                    now
                );
                errors.AddRange(validation.Errors.Where(error => !errors.Contains(error)));
                values = validation.Parsed;
            }

            result.Add(
                new CollectionRow(
                    line,
                    characteristic,
                    instrument,
                    collectedAt,
                    rawValues,
                    values,
                    shift.Length > 0 ? shift : null,
                    NullIfEmpty(Text(row, "lote")),
                    NullIfEmpty(Text(row, "observacao")),
                    errors
                )
            );
        }
        return result;
    }

    public async Task<ImportBatch> CommitAsync(
        string fileName,
        byte[] bytes,
        string actor,
        CancellationToken cancellationToken = default
    )
    {
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var existing = await db.ImportBatches.FirstOrDefaultAsync(
            x => x.FileSha256 == digest,
            cancellationToken
        );
        if (existing is not null)
        {
            var uploader = await db
                .Users.Where(user => user.Id == existing.UploadedBy)
                .Select(user => user.FullName)
                .FirstOrDefaultAsync(cancellationToken);
            throw new ArgumentException(
                $"Este arquivo já foi importado em {LocalTime.Format(existing.UploadedAt)} por {uploader ?? existing.UploadedBy}."
            );
        }

        // Never trust the preview the browser saw: parse and validate again here.
        var parsed = await ParseAsync(fileName, new MemoryStream(bytes), cancellationToken);
        var rows = await PreviewAsync(parsed, cancellationToken);
        var valid = rows.Where(row => row.IsValid).ToList();

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var batch = new ImportBatch
        {
            FileName = Truncate(Path.GetFileName(fileName), 255),
            UploadedBy = actor,
            UploadedAt = DateTimeOffset.UtcNow,
            RowsTotal = rows.Count,
            RowsImported = valid.Count,
            RowsRejected = rows.Count - valid.Count,
            FileSha256 = digest,
            Status = valid.Count == rows.Count ? "completed" : "completed_with_errors",
        };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync(cancellationToken);

        foreach (var row in valid)
        {
            db.Subgroups.Add(
                new Subgroup
                {
                    CharacteristicId = row.Characteristic!.Id,
                    CharacteristicRevision = row.Characteristic.Revision,
                    InstrumentId = row.Instrument?.Id,
                    OperatorId = actor,
                    CollectedAt = row.CollectedAt!.Value,
                    Shift = row.Shift,
                    Lot = row.Lot,
                    Note = row.Note,
                    Source = "upload",
                    ImportBatchId = batch.Id,
                    Measurements = row
                        .Values.Select(
                            (value, index) =>
                                new Measurement { Position = index + 1, Value = value }
                        )
                        .ToList(),
                }
            );
        }
        await db.SaveChangesAsync(cancellationToken);
        await audit.RecordAsync(
            actor,
            "import_batch",
            batch.Id.ToString(CultureInfo.InvariantCulture),
            "import.commit",
            after: new
            {
                batch.FileName,
                batch.RowsTotal,
                batch.RowsImported,
                batch.RowsRejected,
                batch.Status,
            },
            cancellationToken: cancellationToken
        );
        await transaction.CommitAsync(cancellationToken);
        return batch;
    }

    /// <summary>Reads the "Coleta" sheet as text cells (dates become "dd/MM/yyyy HH:mm").</summary>
    private static List<string[]> ReadXlsx(byte[] bytes)
    {
        try
        {
            using var workbook = new XLWorkbook(new MemoryStream(bytes));
            if (!workbook.TryGetWorksheet("Coleta", out var sheet))
                throw new ArgumentException("A planilha precisa ter a aba \"Coleta\".");
            var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 0;
            var table = new List<string[]>();
            for (var r = 1; r <= lastRow; r++)
            {
                var cells = new string[lastColumn];
                for (var c = 1; c <= lastColumn; c++)
                {
                    var cell = sheet.Cell(r, c);
                    cells[c - 1] =
                        cell.DataType == XLDataType.DateTime
                            ? cell.GetDateTime()
                                .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
                            : cell.GetFormattedString().Trim();
                }
                table.Add(cells);
            }
            return table;
        }
        catch (ArgumentException)
        {
            throw;
        }
        catch (Exception)
        {
            throw new ArgumentException("Arquivo XLSX inválido ou corrompido.");
        }
    }

    private static List<string[]> ReadCsv(byte[] bytes)
    {
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            text = Encoding.GetEncoding(1252).GetString(bytes);
        }
        text = text.TrimStart('﻿');
        var sample = text.Length > 4096 ? text[..4096] : text;
        var delimiter = sample.Count(ch => ch == ';') >= sample.Count(ch => ch == ',') ? ';' : ',';
        return text.Split(["\r\n", "\n"], StringSplitOptions.None)
            .Select(line => line.Split(delimiter).Select(cell => cell.Trim()).ToArray())
            .ToList();
    }

    /// <summary>Turns a header + rows table into dictionaries with a "values" array and "linha".</summary>
    private static List<Dictionary<string, object?>> ToRows(List<string[]> table)
    {
        if (table.Count == 0)
            return [];
        var header = table[0].Select(cell => cell.Trim().ToLowerInvariant()).ToArray();
        if (!RequiredHeaders.All(header.Contains))
            throw new ArgumentException(
                "O arquivo não contém os cabeçalhos obrigatórios do template."
            );
        var valueColumns = header
            .Select((name, index) => (name, index))
            .Where(x => x.name.Length > 1 && x.name[0] == 'v' && int.TryParse(x.name[1..], out _))
            .OrderBy(x => int.Parse(x.name[1..], CultureInfo.InvariantCulture))
            .Select(x => x.index)
            .ToArray();
        if (valueColumns.Length == 0)
            throw new ArgumentException("O arquivo precisa conter colunas v1, v2, ...");

        var rows = new List<Dictionary<string, object?>>();
        for (var r = 1; r < table.Count; r++)
        {
            var cells = table[r];
            if (cells.All(string.IsNullOrWhiteSpace))
                continue;
            string Cell(int index) => index < cells.Length ? cells[index] : "";
            var row = new Dictionary<string, object?>();
            for (var c = 0; c < header.Length; c++)
                row[header[c]] = Cell(c);
            // The template ships one sample row marked "Exemplo"; it is never imported.
            if (
                string.Equals(
                    Text(row, "observacao"),
                    "Exemplo",
                    StringComparison.OrdinalIgnoreCase
                )
            )
                continue;
            row["values"] = valueColumns.Select(Cell).ToArray();
            row["linha"] = r + 1;
            rows.Add(row);
            if (rows.Count > MaxRows)
                throw new ArgumentException("O arquivo excede o limite de 5000 linhas de dados.");
        }
        return rows;
    }

    private static string Text(Dictionary<string, object?> row, string key) =>
        row.TryGetValue(key, out var value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? ""
            : "";

    private static int? ParseInt(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

    private static string? NullIfEmpty(string text) => text.Length > 0 ? text : null;

    private static string Truncate(string text, int length) =>
        text.Length <= length ? text : text[..length];
}
