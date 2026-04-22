using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Application.Abstractions.Services;
using Domain.Abstractions.Clients;
using Infrastructure.Clients;
using Infrastructure.Services;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<MoneyWizardContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<Domain.Abstractions.IUnitOfWork, Data.UnitOfWork>();
        services.AddScoped<Domain.Abstractions.Repositories.IUserRepository, Data.Repositories.UserRepository>();
        services.AddScoped<Domain.Abstractions.Repositories.IPaycheckRepository, Data.Repositories.PaycheckRepository>();
        services.AddScoped<Domain.Abstractions.Repositories.IInvoiceRepository, Data.Repositories.InvoiceRepository>();
        services.AddScoped<Domain.Abstractions.Repositories.IExpenseRepository, Data.Repositories.ExpenseRepository>();
        services.AddScoped<Domain.Abstractions.Repositories.IExpenseCategoryRepository, Data.Repositories.ExpenseCategoryRepository>();
        services.AddScoped<Domain.Abstractions.Repositories.IInvoiceCategoryRepository, Data.Repositories.InvoiceCategoryRepository>();
        services.AddScoped<Domain.Abstractions.Repositories.IArgentinaUserProfileRepository, Data.Repositories.ArgentinaUserProfileRepository>();

        services.AddExchangeRates(configuration);

        return services;
    }

    private static IServiceCollection AddExchangeRates(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var baseAddress = configuration["ExchangeRates:ArgentinaDatos:BaseAddress"]
            ?? "https://api.argentinadatos.com/";

        services.AddHttpClient<IExchangeRateClient, ArgentinaDatosExchangeRateClient>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<IExchangeRateCache, ExchangeRateCache>();

        return services;
    }

    public static IApplicationBuilder UseMigrations(this IApplicationBuilder app)
    {
        using var scope = app.ApplicationServices.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MoneyWizardContext>();
        context.Database.SetCommandTimeout(TimeSpan.FromMinutes(10));
        context.Database.Migrate();
        return app;
    }
}
