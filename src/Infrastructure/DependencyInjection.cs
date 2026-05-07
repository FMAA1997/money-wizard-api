using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Data;
using Microsoft.AspNetCore.Builder;
using Application.Abstractions.Pricing;
using Application.Abstractions.Services;
using Domain.Abstractions.Clients;
using Infrastructure.Clients;
using Infrastructure.Pricing;
using Infrastructure.Pricing.Providers;
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
        services.AddScoped<Domain.Abstractions.Repositories.IInvestmentRepository, Data.Repositories.InvestmentRepository>();

        services.AddHttpClients(configuration);
        services.AddHttpClientTo<IExchangeRateClient, ArgentinaDatosExchangeRateClient>("ArgentinaDatos");
        services.AddHttpClientTo<IArgentinaDatosClient, ArgentinaDatosClient>("ArgentinaDatos");
        services.AddHttpClientTo<IDolarApiClient, DolarApiClient>("DolarApi");
        services.AddHttpClientTo<IData912Client, Data912Client>("Data912");
        services.AddHttpClientTo<ICoinGeckoClient, CoinGeckoClient>("CoinGecko");
        services.AddHttpClientTo<IFinnhubClient, FinnhubClient>("Finnhub");
        services.AddHttpClientTo<ICafciClient, CafciClient>("Cafci");

        services.AddSingleton<IExchangeRateCache, ExchangeRateCache>();
        services.AddSingleton<Data912SnapshotCache>();

        services.AddSingleton<IPriceProvider, FinnhubUsStocksProvider>();
        services.AddSingleton<IPriceProvider, FinnhubEtfProvider>();
        services.AddSingleton<IPriceProvider, Data912CedearsProvider>();
        services.AddSingleton<IPriceProvider, Data912ArgBondsProvider>();
        services.AddSingleton<IPriceProvider, CoinGeckoCryptoProvider>();
        services.AddSingleton<IPriceProvider, CafciFciProvider>();
        services.AddSingleton<IPriceProviderRegistry, PriceProviderRegistry>();

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

    private static IServiceCollection AddHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new List<ClientSettings>();
        configuration.Bind("ClientSettings", settings);

        foreach (var setting in settings)
        {
            services.AddHttpClient(setting.Name, client => client.Build(setting));
        }

        return services;
    }

    private static IHttpClientBuilder AddHttpClientTo<TService, TImplementation>(
        this IServiceCollection services,
        string clientName)
        where TService : class
        where TImplementation : class, TService =>
        services.AddHttpClient<TService, TImplementation>(clientName);

    private static HttpClient Build(this HttpClient client, ClientSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.BaseAddress))
            client.BaseAddress = new Uri(settings.BaseAddress);

        client.Timeout = TimeSpan.FromSeconds(30);

        foreach (var header in settings.Header)
            client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);

        return client;
    }
}
