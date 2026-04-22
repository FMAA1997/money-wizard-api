using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class InvestmentConfiguration : IEntityTypeConfiguration<Investment>
{
    public void Configure(EntityTypeBuilder<Investment> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.AssetClass)
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(i => i.Ticker)
            .HasMaxLength(32);

        builder.Property(i => i.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.Quantity)
            .HasPrecision(28, 10);

        builder.Property(i => i.ManualYield)
            .HasPrecision(10, 4);

        builder.HasOne(i => i.User)
            .WithMany(u => u.Investments)
            .HasForeignKey(i => i.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.UserId);
    }
}
