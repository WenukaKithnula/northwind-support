using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using SupportDesk.Application.Abstractions;
using SupportDesk.Presentation.Security;

namespace SupportDesk.Presentation.Extensions;

public static class AuthenticationExtensions
{
    /// <summary>
    /// Signs tokens, checks them on every request, and closes the whole API to anyone without one.
    /// </summary>
    /// <remarks>
    /// The closing is a single fallback policy: every endpoint requires a signed-in agent unless it
    /// opts out with <c>[AllowAnonymous]</c>. Nothing has to remember to ask, so a new endpoint is
    /// closed by default and only login is left open.
    /// </remarks>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IValidateOptions<JwtSettings>, JwtSettingsValidator>();

        services.AddOptions<JwtSettings>()
            .Bind(configuration.GetSection(JwtSettings.SectionName))
            .ValidateOnStart();

        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtSettings>>((bearer, jwt) =>
            {
                var settings = jwt.Value;

                // Keep the claim names as they are in the token ("sub", "name"), not renamed to .NET's.
                bearer.MapInboundClaims = false;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,
                    ValidateAudience = true,
                    ValidAudience = settings.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };

                // A rejected call gets the same problem document as every other error.
                bearer.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;

                        await context.Response.WriteAsJsonAsync(new ProblemDetails
                        {
                            Title = "Unauthorized",
                            Status = StatusCodes.Status401Unauthorized,
                            Detail = "Sign in to use this API."
                        });
                    }
                };
            });

        services.AddAuthorization(options =>
            options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
