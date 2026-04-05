using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ExpenseCategoryConfiguration : IEntityTypeConfiguration<ExpenseCategory>
{
    public void Configure(EntityTypeBuilder<ExpenseCategory> builder)
    {
        builder.HasKey(ec => ec.Id);

        builder.Property(ec => ec.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(ec => ec.Color)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasOne(ec => ec.User)
            .WithMany(u => u.ExpenseCategories)
            .HasForeignKey(ec => ec.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
