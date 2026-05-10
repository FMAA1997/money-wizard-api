using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class InvoiceSegmentConfiguration : IEntityTypeConfiguration<InvoiceSegment>
{
    public void Configure(EntityTypeBuilder<InvoiceSegment> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Amount)
            .HasPrecision(18, 2);

        builder.Property(s => s.Currency)
            .IsRequired()
            .HasMaxLength(3);

        builder.HasOne(s => s.Series)
            .WithMany(i => i.Segments)
            .HasForeignKey(s => s.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsOne(s => s.RecurrenceRule, rule =>
        {
            rule.Property(r => r.Frequency).HasColumnName("RecurrenceFrequency");
            rule.Property(r => r.Interval).HasColumnName("RecurrenceInterval");
            rule.Property(r => r.EndDate).HasColumnName("RecurrenceEndDate");
            rule.Property(r => r.TotalInstallments).HasColumnName("RecurrenceTotalInstallments");
        });

        builder.HasOne(s => s.PaycheckSeries)
            .WithMany()
            .HasForeignKey(s => s.Source)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(s => new { s.SeriesId, s.EffectiveFrom })
            .IsUnique();
    }
}
