using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class PaycheckSeriesConfiguration : IEntityTypeConfiguration<PaycheckSeries>
{
    public void Configure(EntityTypeBuilder<PaycheckSeries> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne(s => s.User)
            .WithMany(u => u.PaycheckSeries)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
