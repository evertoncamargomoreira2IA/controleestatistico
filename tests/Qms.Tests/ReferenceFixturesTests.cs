using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Qms.Infrastructure.Persistence;

namespace Qms.Tests;

public sealed class ReferenceFixturesTests
{
    [Theory]
    [InlineData("AT-001", 326.95, 326.65, 327.25)]
    [InlineData("AT-002", 4.68, 4.58, 4.78)]
    [InlineData("AT-003", -5.25, -6, -4.5)]
    [InlineData("AT-004", 49.39, 49.29, 49.49)]
    [InlineData("AT-005", 1.11, 1.01, 1.21)]
    [InlineData("PK-001", 64.13, 63.93, 64.33)]
    [InlineData("PK-002", 4.73, 4.33, 4.83)]
    [InlineData("PK-003", 317.28, 317.18, 317.58)]
    [InlineData("PK-004", 10.09, 9.89, 10.29)]
    [InlineData("PK-005", 40.45, 40.15, 40.75)]
    [InlineData("PK-006", 48.63, 48.43, 48.83)]
    public void Specifications_match_user_image_including_negative_and_asymmetric_limits(
        string code,
        double nominal,
        double lsl,
        double usl
    )
    {
        var fixture = Assert.Single(ReferenceFixtures.All, x => x.Code == code);
        Assert.Equal(nominal, fixture.Nominal, 6);
        Assert.Equal(lsl, fixture.Lsl, 6);
        Assert.Equal(usl, fixture.Usl, 6);
    }

    [Fact]
    public async Task Seeding_is_idempotent_and_does_not_overwrite_engineering_revisions()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var db = new QmsDbContext(
            new DbContextOptionsBuilder<QmsDbContext>().UseSqlite(connection).Options
        );
        await db.Database.EnsureCreatedAsync();
        await ReferenceFixtures.SeedAsync(db);
        var characteristic = await db.Characteristics.SingleAsync(x => x.Nominal == -5.25);
        characteristic.UpdateSpecification(
            -6.1,
            -5.25,
            -4.4,
            3,
            Qms.Domain.MasterData.ChartType.XbarR
        );
        await db.SaveChangesAsync();
        await ReferenceFixtures.SeedAsync(db);
        Assert.Equal(11, await db.Toolings.CountAsync());
        Assert.Equal(11, await db.Characteristics.CountAsync());
        Assert.Equal(2, await db.Products.CountAsync());
        Assert.Equal(-6.1, characteristic.Lsl);
        Assert.Equal(2, characteristic.Revision);
        Assert.All(
            await db.Characteristics.ToListAsync(),
            x =>
            {
                Assert.Equal(3, x.SubgroupSize);
                Assert.Null(x.Validate());
            }
        );
    }
}
