using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Abstractions.Services.IAuthService, Services.AuthService>();
        services.AddScoped<Abstractions.Services.IPaycheckService, Services.PaycheckService>();
        services.AddScoped<Abstractions.Services.IInvoiceService, Services.InvoiceService>();
        services.AddScoped<Abstractions.Services.IExpenseService, Services.ExpenseService>();
        services.AddScoped<Abstractions.Services.IExpenseCategoryService, Services.ExpenseCategoryService>();
        services.AddScoped<Abstractions.Services.IInvoiceCategoryService, Services.InvoiceCategoryService>();
        services.AddScoped<Abstractions.Services.IPaycheckStatisticsService, Services.PaycheckStatisticsService>();
        services.AddScoped<Abstractions.Services.IExpenseStatisticsService, Services.ExpenseStatisticsService>();
        services.AddScoped<Abstractions.Services.IArgentinaUserProfileService, Services.ArgentinaUserProfileService>();

        return services;
    }
}
