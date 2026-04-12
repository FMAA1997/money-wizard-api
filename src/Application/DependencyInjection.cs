using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Abstractions.Services.IAuthService, Services.AuthService>();
        services.AddScoped<Abstractions.Services.IPaycheckService, Services.PaycheckService>();
        services.AddScoped<Abstractions.Services.IExpenseService, Services.ExpenseService>();
        services.AddScoped<Abstractions.Services.IExpenseCategoryService, Services.ExpenseCategoryService>();
        services.AddScoped<Abstractions.Services.IPaycheckStatisticsService, Services.PaycheckStatisticsService>();
        services.AddScoped<Abstractions.Services.IExpenseStatisticsService, Services.ExpenseStatisticsService>();

        return services;
    }
}
