using Microsoft.Extensions.DependencyInjection;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Abstractions.Services.IAuthService, Services.AuthService>();
        services.AddScoped<Abstractions.Services.IPaycheckService, Services.PaycheckService>();

        return services;
    }
}
