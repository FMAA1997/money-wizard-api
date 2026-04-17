using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ArgentinaUserProfileConfiguration : IEntityTypeConfiguration<ArgentinaUserProfile>
{
    public void Configure(EntityTypeBuilder<ArgentinaUserProfile> builder)
    {
        builder.HasKey(p => p.Id);

        builder.HasIndex(p => p.UserId)
            .IsUnique();

        builder.Property(p => p.EmploymentStatus)
            .IsRequired()
            .HasMaxLength(32);

        builder.HasOne(p => p.MonotributoCategory)
            .WithMany()
            .HasForeignKey(p => p.MonotributoCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
