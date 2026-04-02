using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class PaycheckDistributionConfiguration : IEntityTypeConfiguration<PaycheckDistribution>
{
    public void Configure(EntityTypeBuilder<PaycheckDistribution> builder)
    {
        builder.HasKey(pd => pd.Id);

        builder.Property(pd => pd.Amount)
            .HasPrecision(18, 2);

        builder.Property(pd => pd.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne(pd => pd.Paycheck)
            .WithMany(p => p.Distributions)
            .HasForeignKey(pd => pd.Source)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
