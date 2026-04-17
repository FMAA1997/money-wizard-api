using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class InvoiceCategoryConfiguration : IEntityTypeConfiguration<InvoiceCategory>
{
    public void Configure(EntityTypeBuilder<InvoiceCategory> builder)
    {
        builder.HasKey(ic => ic.Id);

        builder.Property(ic => ic.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ic => ic.Country)
            .IsRequired()
            .HasMaxLength(10);

        builder.Property(ic => ic.Type)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ic => ic.Bottom)
            .HasPrecision(18, 2);

        builder.Property(ic => ic.Top)
            .HasPrecision(18, 2);

        builder.Property(ic => ic.Tax)
            .HasPrecision(18, 2);
    }
}
