using System.IdentityModel.Tokens.Jwt;
using System.Text;
using FCG.Application.Identity;
using FCG.Domain.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FCG.Infrastructure.Identity;

public static class IdentityModule
{
    public static IServiceCollection AddIdentityModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidateOptions<AdminBootstrapOptions>, AdminBootstrapOptionsValidator>();
        services
            .AddOptions<AdminBootstrapOptions>()
            .Bind(configuration.GetSection(AdminBootstrapOptions.SectionName));

        services.AddSingleton<IPasswordHasher, AspNetCorePasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<RegisterUserHandler>();
        services.AddScoped<LoginUserHandler>();
        services.AddScoped<GetCurrentUserHandler>();
        services.AddScoped<AdminBootstrapService>();
        services.AddHostedService<AdminBootstrapHostedService>();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();
        services
            .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>(
                (bearerOptions, jwtOptions) =>
                {
                    var jwt = jwtOptions.Value;
                    bearerOptions.MapInboundClaims = false;
                    bearerOptions.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,
                        ValidateLifetime = true,
                        RequireExpirationTime = true,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwt.SigningKey)),
                        NameClaimType = JwtRegisteredClaimNames.Sub,
                        RoleClaimType = "role",
                        ClockSkew = TimeSpan.Zero,
                    };
                });
        services.AddAuthorization(
            authorizationOptions =>
            {
                authorizationOptions.AddPolicy(
                    IdentityPolicies.UserOrAdmin,
                    policy => policy.RequireRole(
                        nameof(UserRole.User),
                        nameof(UserRole.Administrator)));
                authorizationOptions.AddPolicy(
                    IdentityPolicies.AdminOnly,
                    policy => policy.RequireRole(nameof(UserRole.Administrator)));
            });

        return services;
    }
}
