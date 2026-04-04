using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public sealed class MoneyWizardContext(DbContextOptions<MoneyWizardContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Paycheck> Paychecks => Set<Paycheck>();
    public DbSet<PaycheckDistribution> PaycheckDistributions => Set<PaycheckDistribution>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MoneyWizardContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
