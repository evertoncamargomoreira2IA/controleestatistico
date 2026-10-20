using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Qms.Application.Collection;

namespace Qms.Infrastructure.Collection;

// Direct study uploads are deliberately small and strict. The separate base importer
// continues to accept its full traceability template.
public sealed class StudyFileReader : IStudyFileReader
{
    public async Task<IReadOnlyList<string[]>> ReadAsync(string filename, Stream stream, int maximumSamples)
    {
        if (maximumSamples is not (25 or 33)) throw new ArgumentException("Quantidade de amostras inválida.");
        using var data = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            if (data.Length + read > 5 * 1024 * 1024) throw new ArgumentException("O arquivo excede 5 MB.");
            data.Write(buffer, 0, read);
        }
        data.Position = 0;
        var table = new List<string[]>();
        var ext = Path.GetExtension(filename).ToLowerInvariant();
        if (ext == ".csv")
        {
            using var reader = new StreamReader(data, Encoding.UTF8, true);
            string? line;
            while ((line = await reader.ReadLineAsync()) is not null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (table.Count > maximumSamples) throw new ArgumentException($"O arquivo aceita no máximo {maximumSamples} amostras.");
                table.Add(line.Split(';').Select(x => x.Trim()).ToArray());
            }
        }
        else if (ext == ".xlsx")
        {
            try
            {
                using var book = new XLWorkbook(data);
                var sheet = book.Worksheets.First();
                var last = sheet.LastRowUsed()?.RowNumber() ?? 0;
                if (last > maximumSamples + 1 || (sheet.LastColumnUsed()?.ColumnNumber() ?? 0) != 4)
                    throw new ArgumentException($"Primeira aba: quatro colunas e até {maximumSamples} amostras, além do cabeçalho.");
                for (var row = 1; row <= last; row++)
                {
                    var cells = Enumerable.Range(1, 4).Select(c => sheet.Cell(row, c)).ToArray();
                    if (cells.Any(c => c.HasFormula)) throw new ArgumentException("A aba de leituras aceita valores, sem fórmulas.");
                    table.Add(cells.Select(c => c.DataType == XLDataType.Number ? c.GetDouble().ToString("R", CultureInfo.InvariantCulture) : c.GetString().Trim()).ToArray());
                }
            }
            catch (Exception ex) when (ex is not ArgumentException and not OperationCanceledException and not OutOfMemoryException)
            {
                throw new ArgumentException("Arquivo Excel inválido. Use o modelo XLSX de leituras, sem fórmulas na primeira aba.", ex);
            }
        }
        else throw new ArgumentException("Envie um arquivo CSV ou XLSX do modelo de leituras.");
        if (table.Count < 2 || !table[0].SequenceEqual(new[] { "amostra", "leitura1", "leitura2", "leitura3" }))
            throw new ArgumentException("Cabeçalho obrigatório: amostra;leitura1;leitura2;leitura3.");
        var result = new List<string[]>();
        for (var i = 1; i < table.Count; i++)
        {
            var row = table[i];
            if (row.Length != 4 || row[0] != i.ToString(CultureInfo.InvariantCulture))
                throw new ArgumentException($"Linha {i + 1}: quatro colunas e número da amostra sequencial, começando em 1.");
            if (row.Skip(1).Any(x => !double.TryParse(x.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)))
                throw new ArgumentException($"Amostra {i}: três números finitos são obrigatórios.");
            result.Add(row.Skip(1).ToArray());
        }
        return result;
    }
}
