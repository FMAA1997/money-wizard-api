using Microsoft.AspNetCore.Authentication.JwtBearer;
using Application;
using Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// Add settings variables.
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.local.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"config/appsettings.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

var authentication = builder.Configuration.GetSection("Authentication");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = authentication["Authority"];
        options.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = authentication["Issuer"],
            ValidateAudience = true,
            ValidAudience = authentication["ProjectId"],
            ValidateLifetime = true,
        };
    });

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

builder.Services.AddAuthorization();

builder.Services.AddScoped<Api.Providers.CurrentUserProvider>();
builder.Services.AddScoped<Application.Abstractions.ICurrentUserProvider>(
    sp => sp.GetRequiredService<Api.Providers.CurrentUserProvider>());

builder.Services.AddControllers(options =>
{
    options.Filters.Add<Api.Filters.ResolveCurrentUserFilter>();
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
