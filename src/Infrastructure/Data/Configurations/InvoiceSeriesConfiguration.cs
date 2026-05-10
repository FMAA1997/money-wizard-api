using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class InvoiceSeriesConfiguration : IEntityTypeConfiguration<InvoiceSeries>
{
    public void Configure(EntityTypeBuilder<InvoiceSeries> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.Class)
            .HasConversion<string>()
            .HasMaxLength(1);

        builder.Property(s => s.Type)
            .HasConversion<string>()
            .HasMaxLength(15)
            .HasDefaultValue(InvoiceType.Invoice);

        builder.HasOne(s => s.User)
            .WithMany(u => u.InvoiceSeries)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.ParentException)
            .WithMany(e => e.ChildSeries)
            .HasForeignKey(s => s.ParentExceptionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.ParentExceptionId)
            .HasFilter("\"ParentExceptionId\" IS NOT NULL");
    }
}
