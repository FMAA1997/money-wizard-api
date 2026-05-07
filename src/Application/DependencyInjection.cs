using Application.Abstractions.Services;
using Application.Services;
using Application.Services.CountryHandlers;
using Application.Services.Currency;
using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPaycheckService, PaycheckService>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IExpenseService, ExpenseService>();
        services.AddScoped<IExpenseCategoryService, ExpenseCategoryService>();
        services.AddScoped<IInvoiceCategoryService, InvoiceCategoryService>();
        services.AddScoped<IInvestmentService, InvestmentService>();
        services.AddScoped<IInvestmentCatalogService, InvestmentCatalogService>();
        services.AddScoped<IPaycheckStatisticsService, PaycheckStatisticsService>();
        services.AddScoped<IExpenseStatisticsService, ExpenseStatisticsService>();
        services.AddScoped<IInvoiceStatisticsService, InvoiceStatisticsService>();
        services.AddScoped<IInvestmentStatisticsService, InvestmentStatisticsService>();
        services.AddScoped<IDashboardService, DashboardService>();

        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<ICountryProfileRegistry, CountryProfileRegistry>();
        services.AddScoped<IUserCurrencyContext, UserCurrencyContext>();
        services.AddScoped<ICurrencyConverter, CurrencyConverter>();
        services.AddScoped<UserResponseAssembler>();
        services.AddScoped<ICountryProfileHandler, ArgentinaCountryProfileHandler>();
        services.AddScoped<ICountryProfileHandler, RowCountryProfileHandler>();

        return services;
    }
}
