using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Qms.Application.Collection;
using Qms.Domain.Collection;

namespace Qms.Web.Components.Pages;

public partial class CollectionImport
{
    [Inject]
    private IImportService ImportService { get; set; } = default!;

    [Inject]
    private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    private const long MaxFileBytes = 5 * 1024 * 1024;
    private IReadOnlyList<CollectionRow>? rows;
    private byte[]? fileBytes;
    private string filename = "";
    private string? error;
    private bool committing;
    private ImportBatch? batch;
    private int validCount => rows?.Count(x => x.IsValid) ?? 0;
    private int rejectedCount => rows?.Count(x => !x.IsValid) ?? 0;

    private static string RowStatus(CollectionRow row) =>
        row.IsValid ? "OK" : $"Erro: {string.Join(" ", row.Errors)}";

    private async Task FileSelected(InputFileChangeEventArgs args)
    {
        error = null;
        rows = null;
        batch = null;
        fileBytes = null;
        var file = args.File;
        if (file.Size > MaxFileBytes)
        {
            error = "O arquivo excede o limite de 5 MB.";
            return;
        }
        filename = Path.GetFileName(file.Name);
        if (
            !filename.EndsWith(".csv", StringComparison.OrdinalIgnoreCase)
            && !filename.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
        )
        {
            error = "Envie um arquivo .xlsx ou .csv.";
            return;
        }
        try
        {
            await using var stream = file.OpenReadStream(MaxFileBytes);
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            fileBytes = memory.ToArray();
            memory.Position = 0;
            var parsed = await ImportService.ParseAsync(filename, memory, CancellationToken.None);
            rows = await ImportService.PreviewAsync(parsed, CancellationToken.None);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException)
        {
            error = ex.Message;
            fileBytes = null;
        }
    }

    private async Task CommitAsync()
    {
        if (fileBytes is null)
        {
            error = "Selecione o arquivo novamente.";
            return;
        }
        committing = true;
        error = null;
        try
        {
            var user = (await AuthenticationStateProvider.GetAuthenticationStateAsync()).User;
            batch = await ImportService.CommitAsync(
                filename,
                fileBytes,
                user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "",
                CancellationToken.None
            );
            fileBytes = null;
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
        }
        finally
        {
            committing = false;
        }
    }
}
