using Microsoft.EntityFrameworkCore;
using Qms.Domain.MasterData;

namespace Qms.Infrastructure.Persistence;

public sealed record ReferenceFixture(
    string Product,
    string Code,
    string Description,
    double Nominal,
    double LowerTolerance,
    double UpperTolerance
)
{
    public double Lsl => Math.Round(Nominal - LowerTolerance, 4);
    public double Usl => Math.Round(Nominal + UpperTolerance, 4);
}

public static class ReferenceFixtures
{
    // Transcription of the specification image supplied by the user on 17/10/2026.
    // Identifiers AT-* / PK-* are local demo identifiers, not official tooling codes.
    public static IReadOnlyList<ReferenceFixture> All { get; } =
    [
        new("Athena - Lebi", "AT-001", "Feed Shaft Gear Insert Press Fixture", 326.95, .3, .3),
        new("Athena - Lebi", "AT-002", "Feed Shaft Pulley Insert Press Fixture", 4.68, .1, .1),
        new("Athena - Lebi", "AT-003", "Belt Tension - Paper Motor", -5.25, .75, .75),
        new("Athena - Lebi", "AT-004", "X-Bias Press Fixture", 49.39, .1, .1),
        new("Athena - Lebi", "AT-005", "Out Put Clutch Press Fixture", 1.11, .1, .1),
        new("Peak Mech - Sayan", "PK-001", "Gear Feedroller 24T Press", 64.13, .2, .2),
        new("Peak Mech - Sayan", "PK-002", "Pulley Cluster and Encoder Ring Press", 4.73, .4, .1),
        new("Peak Mech - Sayan", "PK-003", "Gear Right Feedroller 17T Press", 317.28, .1, .3),
        new("Peak Mech - Sayan", "PK-004", "Output Gear 42T Press", 10.09, .2, .2),
        new("Peak Mech - Sayan", "PK-005", "Pick Encoder Disk and Hub Press", 40.45, .3, .3),
        new("Peak Mech - Sayan", "PK-006", "Gear Feedroller 24T Pick & Press", 48.63, .2, .2),
    ];

    public static async Task SeedAsync(QmsDbContext db)
    {
        var area = await db.Areas.SingleOrDefaultAsync(x =>
            x.Name == "Tooling — teste de engenharia"
        );
        if (area is null)
        {
            area = new Area { Name = "Tooling — teste de engenharia" };
            db.Add(area);
            await db.SaveChangesAsync();
        }
        foreach (var group in All.GroupBy(x => x.Product))
        {
            var product = await db.Products.SingleOrDefaultAsync(x =>
                x.AreaId == area.Id && x.Name == group.Key
            );
            if (product is null)
            {
                product = new Product { AreaId = area.Id, Name = group.Key };
                db.Add(product);
                await db.SaveChangesAsync();
            }
            var line = await db.Lines.SingleOrDefaultAsync(x =>
                x.ProductId == product.Id && x.Name == "Linha de teste"
            );
            if (line is null)
            {
                line = new Line { ProductId = product.Id, Name = "Linha de teste" };
                db.Add(line);
                await db.SaveChangesAsync();
            }
            var station = await db.Stations.SingleOrDefaultAsync(x =>
                x.LineId == line.Id && x.Name == "Posto de validação"
            );
            if (station is null)
            {
                station = new Station { LineId = line.Id, Name = "Posto de validação" };
                db.Add(station);
                await db.SaveChangesAsync();
            }
            foreach (var spec in group)
            {
                // Idempotent: existing specifications and revisions are never overwritten.
                var tooling = await db.Toolings.SingleOrDefaultAsync(x => x.Code == spec.Code);
                if (tooling is null)
                {
                    tooling = new Tooling
                    {
                        StationId = station.Id,
                        Code = spec.Code,
                        Name = spec.Description,
                        Description =
                            "Identificador provisório de demonstração. Especificação transcrita da imagem fornecida.",
                    };
                    db.Add(tooling);
                    await db.SaveChangesAsync();
                }
                if (
                    !await db.Characteristics.AnyAsync(x =>
                        x.ToolingId == tooling.Id && x.Name == spec.Description
                    )
                )
                {
                    db.Characteristics.Add(
                        new Characteristic
                        {
                            ToolingId = tooling.Id,
                            Name = spec.Description,
                            Unit = "mm",
                            Nominal = spec.Nominal,
                            Lsl = spec.Lsl,
                            Usl = spec.Usl,
                            SubgroupSize = 3,
                            ChartType = ChartType.XbarR,
                        }
                    );
                    await db.SaveChangesAsync();
                }
            }
        }
    }
}
