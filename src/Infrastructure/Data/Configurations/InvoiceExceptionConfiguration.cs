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
            .IsUnique();
    }
}
