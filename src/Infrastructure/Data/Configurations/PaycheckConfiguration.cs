using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class PaycheckConfiguration : IEntityTypeConfiguration<Paycheck>
{
    public void Configure(EntityTypeBuilder<Paycheck> builder)
    {
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Amount)
            .HasPrecision(18, 2);

        builder.Property(p => p.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne(p => p.User)
            .WithMany(u => u.Paychecks)
            .HasForeignKey(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
