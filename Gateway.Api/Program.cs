using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var jwtSettings = builder.Configuration.GetSection("JwtSettings");

// ================= CORS

// Define the policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("MyGatewayCorsPolicy", policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials(); // REQUIRED for cookies
    });
});

// ================= CORS (apply globally)


// ================= AUTH (validate token ONLY)
// Prevent ASP.NET from writing ANY responses
// =================
builder.Services
.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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

    // 🔥 Disable ALL automatic auth responses
    options.Events = new JwtBearerEvents
    {
        OnChallenge = ctx =>
        {
            ctx.HandleResponse(); // stop 401 body
            return Task.CompletedTask;
        },
        OnForbidden = ctx =>
        {
            ctx.Response.StatusCode = 403;
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = ctx =>
        {
            ctx.NoResult(); // continue to downstream
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();


// ================= YARP =================
builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

// Used Cores
app.UseCors("MyGatewayCorsPolicy");


// ❌ DO NOT USE THESE (they rewrite responses)
// app.UseExceptionHandler();
// app.UseStatusCodePages();
// builder.Services.AddProblemDetails();


// ⭐ THIS LINE FIXES 404 BODY PASS-THROUGH
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


// ⭐ YARP STREAMS EVERYTHING FROM HERE
app.MapReverseProxy();

app.Run();