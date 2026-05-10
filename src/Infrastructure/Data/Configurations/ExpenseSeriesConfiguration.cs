using Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Data.Configurations;

public sealed class ExpenseSeriesConfiguration : IEntityTypeConfiguration<ExpenseSeries>
{
    public void Configure(EntityTypeBuilder<ExpenseSeries> builder)
    {
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Description)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne(s => s.User)
            .WithMany(u => u.ExpenseSeries)
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Category)
            .WithMany(c => c.ExpenseSeries)
            .HasForeignKey(s => s.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
