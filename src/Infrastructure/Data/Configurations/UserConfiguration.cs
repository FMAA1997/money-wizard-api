using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.ExternalId)
            .IsRequired()
            .HasMaxLength(128);

        builder.HasIndex(u => u.ExternalId)
            .IsUnique();

        builder.Property(u => u.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(320);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.Country)
            .IsRequired()
            .HasMaxLength(8)
            .HasDefaultValue("row");

        builder.HasOne(u => u.ArgentinaProfile)
            .WithOne(p => p.User)
            .HasForeignKey<ArgentinaUserProfile>(p => p.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
