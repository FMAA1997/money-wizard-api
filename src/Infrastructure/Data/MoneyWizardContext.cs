using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public sealed class MoneyWizardContext(DbContextOptions<MoneyWizardContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Paycheck> Paychecks => Set<Paycheck>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<InvoiceCategory> InvoiceCategories => Set<InvoiceCategory>();
    public DbSet<ArgentinaUserProfile> ArgentinaUserProfiles => Set<ArgentinaUserProfile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MoneyWizardContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
