using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Infrastructure.Data;

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

        return services;
    }
}
