using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Amount)
            .HasPrecision(18, 2);

        builder.Property(e => e.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.Property(e => e.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne(e => e.User)
            .WithMany(u => u.Expenses)
            .HasForeignKey(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Category)
            .WithMany(c => c.Expenses)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Paycheck)
            .WithMany(p => p.Expenses)
            .HasForeignKey(e => e.PaycheckId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Invoice)
            .WithMany(i => i.Expenses)
            .HasForeignKey(e => e.InvoiceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Recurrence rule (owned type — columns on same table)
        builder.OwnsOne(e => e.RecurrenceRule, rule =>
        {
            rule.Property(r => r.Frequency).HasColumnName("RecurrenceFrequency");
            rule.Property(r => r.Interval).HasColumnName("RecurrenceInterval");
            rule.Property(r => r.EndDate).HasColumnName("RecurrenceEndDate");
            rule.Property(r => r.TotalInstallments).HasColumnName("RecurrenceTotalInstallments");
        });

        // Self-referential FK for overrides/exceptions
        builder.HasOne(e => e.RecurringExpense)
            .WithMany(e => e.Exceptions)
            .HasForeignKey(e => e.RecurringExpenseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        // One override per occurrence date per series
        builder.HasIndex(e => new { e.RecurringExpenseId, e.OriginalDate })
            .IsUnique()
            .HasFilter("\"RecurringExpenseId\" IS NOT NULL");
    }
}
