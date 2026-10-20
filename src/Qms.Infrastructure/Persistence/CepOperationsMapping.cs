using Microsoft.EntityFrameworkCore;
using Qms.Domain.Collection;

namespace Qms.Infrastructure.Persistence;

public sealed partial class CepOperationsMapping
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<CepPlan>(e =>
        {
            e.ToTable("cep_plans");
            e.HasKey(x => x.Id);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.Title).HasMaxLength(120);
            e.Property(x => x.Day).HasMaxLength(10);
            e.Property(x => x.Shift).HasMaxLength(2);
            e.Property(x => x.CharacteristicName).HasMaxLength(120);
            e.Property(x => x.ProcessPath).HasMaxLength(1000);
            e.Property(x => x.ResponsibleId).HasMaxLength(450);
            e.Property(x => x.CreatedBy).HasMaxLength(450);
            e.HasIndex(x => x.Day);
            e.HasOne<Qms.Domain.MasterData.Characteristic>()
                .WithMany()
                .HasForeignKey(x => x.CharacteristicId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CepPlanDataset>(e =>
        {
            e.ToTable("cep_plan_datasets");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.DatasetId).IsUnique();
            e.Property(x => x.LinkedBy).HasMaxLength(450);
            e.HasOne<CepPlan>()
                .WithMany()
                .HasForeignKey(x => x.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CepDataset>()
                .WithMany()
                .HasForeignKey(x => x.DatasetId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CepLimitApproval>(e =>
        {
            e.ToTable("cep_limit_approvals");
            e.HasKey(x => x.Id);
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.ResponsibleId).HasMaxLength(450);
            e.HasIndex(x => x.SourceAnalysisId).IsUnique();
            e.HasOne<CepAnalysis>()
                .WithMany()
                .HasForeignKey(x => x.SourceAnalysisId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CepLimitRevocation>(e =>
        {
            e.ToTable("cep_limit_revocations");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ApprovalId).IsUnique();
            e.Property(x => x.Reason).HasMaxLength(1000);
            e.Property(x => x.ActorId).HasMaxLength(450);
            e.HasOne<CepLimitApproval>()
                .WithMany()
                .HasForeignKey(x => x.ApprovalId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CepOccurrence>(e =>
        {
            e.ToTable("cep_occurrences");
            e.HasKey(x => x.Id);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.AnalysisId).IsUnique();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Cause).HasMaxLength(2000);
            e.Property(x => x.Action).HasMaxLength(2000);
            e.Property(x => x.Verification).HasMaxLength(2000);
            e.Property(x => x.Status).HasMaxLength(30);
            e.Property(x => x.ResponsibleId).HasMaxLength(450);
            e.HasOne<CepAnalysis>()
                .WithMany()
                .HasForeignKey(x => x.AnalysisId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
