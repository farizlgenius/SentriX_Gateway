using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

namespace Gateway.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var jwtSettings = builder.Configuration.GetSection("JwtSettings");

        // ==========================
        // Authentication (JWT)
        // ==========================
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = jwtSettings["Authority"];
                options.Audience = jwtSettings["Audience"];
                options.RequireHttpsMetadata = false;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidateIssuer = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true
                };
            });

        // ==========================
        // Authorization Policies
        // ==========================
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("UserPolicy", p => p.RequireAuthenticatedUser());
            options.AddPolicy("IntegrationPolicy", p => p.RequireAuthenticatedUser());
        });

        // ==========================
        // YARP Reverse Proxy
        // ==========================
        builder.Services
           .AddReverseProxy()
           .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

        // ==========================
        // CORS
        // ==========================
        builder.Services.AddCors(opt =>
        {
            opt.AddPolicy("AllowAll", policy =>
                policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
        });

        builder.Services.AddHealthChecks();

        // ==========================
        // ⭐ SWAGGER (Swashbuckle)
        // ==========================
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Gateway API",
                Version = "v1"
            });

            // 🔐 JWT support inside Swagger UI
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste JWT token here"
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    Array.Empty<string>()
                }
            });
        });

        // ==========================
        // Rate Limiting
        // ==========================
        builder.Services.AddRateLimiter(options =>
        {
            options.AddFixedWindowLimiter("fixed", config =>
            {
                config.Window = TimeSpan.FromSeconds(10);
                config.PermitLimit = 100;
            });
        });

        var app = builder.Build();

        // ==========================
        // Middleware pipeline
        // ==========================
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();
        app.UseCors("AllowAll");

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapReverseProxy();

        // Security headers
        app.Use(async (context, next) =>
        {
            context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
            context.Response.Headers.Append("X-Frame-Options", "DENY");
            await next();
        });

        app.Run();
    }
}