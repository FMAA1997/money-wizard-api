using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class PaycheckConfiguration : IEntityTypeConfiguration<Paycheck>
{
    public void Configure(EntityTypeBuilder<Paycheck> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount)
            .HasPrecision(18, 2);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne(p => p.User)
            .WithMany(u => u.Paychecks)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Recurrence rule (owned type — columns on same table)
        builder.OwnsOne(p => p.RecurrenceRule, rule =>
        {
            rule.Property(r => r.Frequency).HasColumnName("RecurrenceFrequency");
            rule.Property(r => r.Interval).HasColumnName("RecurrenceInterval");
            rule.Property(r => r.EndDate).HasColumnName("RecurrenceEndDate");
            rule.Property(r => r.TotalInstallments).HasColumnName("RecurrenceTotalInstallments");
        });

        // Self-referential FK for overrides/exceptions
        builder.HasOne(p => p.RecurringPaycheck)
            .WithMany(p => p.Exceptions)
            .HasForeignKey(p => p.RecurringPaycheckId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.IsDeleted).HasDefaultValue(false);

        // One override per occurrence date per series
        builder.HasIndex(p => new { p.RecurringPaycheckId, p.OriginalDate })
            .IsUnique()
            .HasFilter("\"RecurringPaycheckId\" IS NOT NULL");
    }
}
