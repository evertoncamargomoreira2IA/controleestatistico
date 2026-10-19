using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Qms.Domain.Auditing;
using Qms.Domain.Collection;
using Qms.Domain.MasterData;
using Qms.Domain.Settings;
using Qms.Infrastructure.Identity;

namespace Qms.Infrastructure.Persistence;

public sealed class QmsDbContext(DbContextOptions<QmsDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CepDataset> CepDatasets => Set<CepDataset>();
    public DbSet<CepSample> CepSamples => Set<CepSample>();
    public DbSet<CepAnalysis> CepAnalyses => Set<CepAnalysis>();
    public DbSet<Subgroup> Subgroups => Set<Subgroup>();
    public DbSet<Measurement> Measurements => Set<Measurement>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Line> Lines => Set<Line>();
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<Tooling> Toolings => Set<Tooling>();
    public DbSet<Characteristic> Characteristics => Set<Characteristic>();
    public DbSet<Instrument> Instruments => Set<Instrument>();
    public DbSet<SettingsVersion> SettingsVersions => Set<SettingsVersion>();
    public DbSet<SettingValue> SettingValues => Set<SettingValue>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAuditLogsAreAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default
    )
    {
        EnsureAuditLogsAreAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureAuditLogsAreAppendOnly()
    {
        if (ChangeTracker.Entries<CepAnalysis>().Any(x => x.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Análises CEP salvas são somente para inclusão.");
        if (
            ChangeTracker
                .Entries<AuditLog>()
                .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted)
        )
            throw new InvalidOperationException(
                "Registros de auditoria são somente para inclusão."
            );
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        // SQLite cannot compare or order DateTimeOffset values in SQL. Store them as UTC
        // ticks (sortable integers) there; SQL Server keeps its native datetimeoffset type.
        if (Database.IsSqlite())
        {
            configurationBuilder
                .Properties<DateTimeOffset>()
                .HaveConversion<DateTimeOffsetToBinaryConverter>();
            configurationBuilder
                .Properties<DateTimeOffset?>()
                .HaveConversion<DateTimeOffsetToBinaryConverter>();
        }
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<CepAnalysis>(e =>
        {
            e.ToTable("cep_analyses");
            e.HasKey(x => x.Id);
            e.Property(x => x.Method).HasMaxLength(80).IsRequired();
            e.Property(x => x.CreatedBy).HasMaxLength(450).IsRequired();
            e.Property(x => x.InputJson).IsRequired();
            e.Property(x => x.ResultJson).IsRequired();
            e.HasIndex(x => new { x.DatasetId, x.DatasetVersion, x.Method }).IsUnique();
            e.HasOne<CepDataset>().WithMany().HasForeignKey(x => x.DatasetId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Entity<ApplicationUser>(user =>
            user.Property(x => x.FullName).HasMaxLength(200).IsRequired()
        );
        builder.Entity<AuditLog>(audit =>
        {
            audit.ToTable("audit_log");
            audit.HasKey(x => x.Id);
            audit.Property(x => x.Entity).HasMaxLength(100).IsRequired();
            audit.Property(x => x.EntityId).HasMaxLength(100);
            audit.Property(x => x.Action).HasMaxLength(80).IsRequired();
            audit.Property(x => x.IpAddress).HasMaxLength(64);
            audit.Property(x => x.Origin).HasMaxLength(100);
            audit.Property(x => x.OccurredAt).IsRequired();
            audit.HasIndex(x => x.OccurredAt);
            audit.HasIndex(x => x.Entity);
        });
        ConfigureMasterData(builder);
        ConfigureCollection(builder);
        ConfigureCep(builder);
    }

    private static void ConfigureCep(ModelBuilder builder)
    {
        var sets = builder.Entity<CepDataset>();
        sets.ToTable("cep_datasets");
        sets.HasKey(x => x.Id);
        sets.Property(x => x.Version).IsConcurrencyToken();
        sets.Property(x => x.Title).HasMaxLength(120).IsRequired();
        sets.Property(x => x.CreatedBy).HasMaxLength(450).IsRequired();
        sets.Property(x => x.CharacteristicName).HasMaxLength(120).IsRequired();
        sets.Property(x => x.ProcessPath).HasMaxLength(1000).IsRequired();
        sets.Property(x => x.Unit).HasMaxLength(40);
        sets.HasIndex(x => x.CreatedAt);
        sets.HasOne<Characteristic>()
            .WithMany()
            .HasForeignKey(x => x.CharacteristicId)
            .OnDelete(DeleteBehavior.Restrict);
        sets.Ignore(x => x.CompleteSamples).Ignore(x => x.Readings).Ignore(x => x.ProgressPercent);
        sets.HasMany(x => x.Samples)
            .WithOne()
            .HasForeignKey(x => x.CepDatasetId)
            .OnDelete(DeleteBehavior.Cascade);
        var rows = builder.Entity<CepSample>();
        rows.ToTable("cep_samples");
        rows.HasKey(x => x.Id);
        rows.HasIndex(x => new { x.CepDatasetId, x.Position }).IsUnique();
        rows.Property(x => x.Shift).HasMaxLength(10).IsRequired();
        rows.Property(x => x.Lot).HasMaxLength(80).IsRequired();
        rows.Property(x => x.OperatorId).HasMaxLength(450).IsRequired();
        rows.Property(x => x.Source).HasMaxLength(20).IsRequired();
        rows.Ignore(x => x.IsComplete).Ignore(x => x.Mean).Ignore(x => x.Range);
        rows.HasOne<Instrument>()
            .WithMany()
            .HasForeignKey(x => x.InstrumentId)
            .OnDelete(DeleteBehavior.Restrict);
        rows.HasOne<Subgroup>()
            .WithMany()
            .HasForeignKey(x => x.SourceSubgroupId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureCollection(ModelBuilder builder)
    {
        var batches = builder.Entity<ImportBatch>();
        batches.ToTable("import_batches");
        batches.HasKey(x => x.Id);
        batches.Property(x => x.FileName).HasMaxLength(255).IsRequired();
        batches.Property(x => x.UploadedBy).HasMaxLength(450).IsRequired();
        batches.Property(x => x.FileSha256).HasMaxLength(64).IsRequired();
        batches.HasIndex(x => x.FileSha256).IsUnique();
        var subgroups = builder.Entity<Subgroup>();
        subgroups.ToTable("subgroups");
        subgroups.HasKey(x => x.Id);
        subgroups.Property(x => x.OperatorId).HasMaxLength(450).IsRequired();
        subgroups.Property(x => x.Source).HasMaxLength(20).IsRequired();
        subgroups.Property(x => x.Shift).HasMaxLength(10);
        subgroups.Property(x => x.Lot).HasMaxLength(80);
        subgroups.Property(x => x.ExclusionReason).HasMaxLength(300);
        subgroups.Property(x => x.Note).HasMaxLength(500);
        subgroups
            .HasIndex(x => new { x.CharacteristicId, x.CollectedAt })
            .HasDatabaseName("ix_subgroups_characteristic_collected_at");
        subgroups
            .HasMany(x => x.Measurements)
            .WithOne()
            .HasForeignKey(x => x.SubgroupId)
            .OnDelete(DeleteBehavior.Cascade);
        subgroups
            .HasOne<ImportBatch>()
            .WithMany(x => x.Subgroups)
            .HasForeignKey(x => x.ImportBatchId);
        var measurements = builder.Entity<Measurement>();
        measurements.ToTable("measurements");
        measurements.HasKey(x => x.Id);
        measurements
            .HasIndex(x => new { x.SubgroupId, x.Position })
            .IsUnique()
            .HasDatabaseName("uq_measurements_subgroup_position");
    }

    private static void ConfigureMasterData(ModelBuilder builder)
    {
        static void Base<TEntity>(
            Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> e,
            string table
        )
            where TEntity : MasterDataEntity
        {
            e.ToTable(table);
            e.HasKey(x => x.Id);
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.IsActive).IsRequired();
        }
        var areas = builder.Entity<Area>();
        Base(areas, "areas");
        areas.Property(x => x.Name).HasMaxLength(120).IsRequired();
        areas.HasIndex(x => x.Name).IsUnique();
        var products = builder.Entity<Product>();
        Base(products, "products");
        products.Property(x => x.Name).HasMaxLength(120).IsRequired();
        products.HasIndex(x => new { x.AreaId, x.Name }).IsUnique();
        var lines = builder.Entity<Line>();
        Base(lines, "lines");
        lines.Property(x => x.Name).HasMaxLength(120).IsRequired();
        lines.HasIndex(x => new { x.ProductId, x.Name }).IsUnique();
        var stations = builder.Entity<Station>();
        Base(stations, "stations");
        stations.Property(x => x.Name).HasMaxLength(120).IsRequired();
        stations.HasIndex(x => new { x.LineId, x.Name }).IsUnique();
        var toolings = builder.Entity<Tooling>();
        Base(toolings, "toolings");
        toolings.Property(x => x.Code).HasMaxLength(80).IsRequired();
        toolings.Property(x => x.Name).HasMaxLength(120).IsRequired();
        toolings.Property(x => x.Description).HasMaxLength(500);
        toolings.HasIndex(x => x.Code).IsUnique();
        var characteristics = builder.Entity<Characteristic>();
        Base(characteristics, "characteristics");
        characteristics.Property(x => x.Name).HasMaxLength(120).IsRequired();
        characteristics.Property(x => x.Unit).HasMaxLength(40);
        characteristics.Property(x => x.DataType).HasConversion<string>().HasMaxLength(20);
        characteristics.Property(x => x.ChartType).HasConversion<string>().HasMaxLength(20);
        characteristics.HasIndex(x => new { x.ToolingId, x.Name }).IsUnique();
        var instruments = builder.Entity<Instrument>();
        Base(instruments, "instruments");
        instruments.Property(x => x.Code).HasMaxLength(80).IsRequired();
        instruments.Property(x => x.Name).HasMaxLength(120).IsRequired();
        instruments.Property(x => x.Type).HasMaxLength(80).IsRequired();
        instruments.Property(x => x.Description).HasMaxLength(500);
        instruments.HasIndex(x => x.Code).IsUnique();
        var versions = builder.Entity<SettingsVersion>();
        versions.ToTable("settings_version");
        versions.HasKey(x => x.Id);
        versions.HasIndex(x => x.Version).IsUnique();
        versions.Property(x => x.Comment).HasMaxLength(1000).IsRequired();
        versions.Property(x => x.CreatedBy).HasMaxLength(450);
        var values = builder.Entity<SettingValue>();
        values.ToTable("setting_value");
        values.HasKey(x => x.Id);
        values.Property(x => x.Key).HasMaxLength(160).IsRequired();
        values.Property(x => x.ValueJson).IsRequired();
        values.HasIndex(x => new { x.SettingsVersionId, x.Key }).IsUnique();
        versions.HasMany(x => x.Values).WithOne().HasForeignKey(x => x.SettingsVersionId);
    }
}
