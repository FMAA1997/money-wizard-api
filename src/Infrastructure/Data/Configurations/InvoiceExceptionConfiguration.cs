using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class InvoiceExceptionConfiguration : IEntityTypeConfiguration<InvoiceException>
{
    public void Configure(EntityTypeBuilder<InvoiceException> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Amount)
            .HasPrecision(18, 2);

        builder.Property(e => e.Currency)
            .HasMaxLength(3);

        builder.HasOne(e => e.Series)
            .WithMany(s => s.Exceptions)
            .HasForeignKey(e => e.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Property(e => e.IsDeleted).HasDefaultValue(false);

        builder.HasIndex(e => new { e.SeriesId, e.OriginalDate })
            .IsUnique()
            .HasFilter("\"OriginalDate\" IS NOT NULL");

        builder.HasIndex(e => new { e.SeriesId, e.Date })
            .IsUnique()
            .HasFilter("\"OriginalDate\" IS NULL");

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "chk_invoice_exception_overlay_kind",
                "\"OriginalDate\" IS NOT NULL OR (\"Date\" IS NOT NULL AND \"Amount\" IS NOT NULL AND \"Currency\" IS NOT NULL)");
            t.HasCheckConstraint(
                "chk_invoice_exception_isdeleted_only_on_override",
                "\"OriginalDate\" IS NOT NULL OR \"IsDeleted\" = FALSE");
        });
    }
}
