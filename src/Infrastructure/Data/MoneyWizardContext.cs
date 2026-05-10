using Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public sealed class MoneyWizardContext(DbContextOptions<MoneyWizardContext> options)
    : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<PaycheckSeries> PaycheckSeries => Set<PaycheckSeries>();
    public DbSet<PaycheckSegment> PaycheckSegments => Set<PaycheckSegment>();
    public DbSet<PaycheckException> PaycheckExceptions => Set<PaycheckException>();
    public DbSet<InvoiceSeries> InvoiceSeries => Set<InvoiceSeries>();
    public DbSet<InvoiceSegment> InvoiceSegments => Set<InvoiceSegment>();
    public DbSet<InvoiceException> InvoiceExceptions => Set<InvoiceException>();
    public DbSet<ExpenseSeries> ExpenseSeries => Set<ExpenseSeries>();
    public DbSet<ExpenseSegment> ExpenseSegments => Set<ExpenseSegment>();
    public DbSet<ExpenseException> ExpenseExceptions => Set<ExpenseException>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<InvoiceCategory> InvoiceCategories => Set<InvoiceCategory>();
    public DbSet<ArgentinaUserProfile> ArgentinaUserProfiles => Set<ArgentinaUserProfile>();
    public DbSet<Investment> Investments => Set<Investment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MoneyWizardContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
