using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.Amount)
            .HasPrecision(18, 2);

        builder.Property(i => i.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne(i => i.User)
            .WithMany(u => u.Invoices)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(i => i.Paycheck)
            .WithMany(p => p.Invoices)
            .HasForeignKey(i => i.Source)
            .OnDelete(DeleteBehavior.SetNull);

        // Recurrence rule (owned type — columns on same table)
        builder.OwnsOne(i => i.RecurrenceRule, rule =>
        {
            rule.Property(r => r.Frequency).HasColumnName("RecurrenceFrequency");
            rule.Property(r => r.Interval).HasColumnName("RecurrenceInterval");
            rule.Property(r => r.EndDate).HasColumnName("RecurrenceEndDate");
            rule.Property(r => r.TotalInstallments).HasColumnName("RecurrenceTotalInstallments");
        });

        // Self-referential FK for overrides/exceptions
        builder.HasOne(i => i.RecurringInvoice)
            .WithMany(i => i.Exceptions)
            .HasForeignKey(i => i.RecurringInvoiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(i => i.IsDeleted).HasDefaultValue(false);

        // One override per occurrence date per series
        builder.HasIndex(i => new { i.RecurringInvoiceId, i.OriginalDate })
            .IsUnique()
            .HasFilter("\"RecurringInvoiceId\" IS NOT NULL");
    }
}
