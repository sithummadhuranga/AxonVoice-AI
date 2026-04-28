using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Repositories;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.AgentConfig.Services;
using AxonVoiceAI.Shared.Configuration;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetRequiredValue(builder.Environment, "POSTGRES_CONNECTION_STRING", "POSTGRES_URL");
var redisConnectionString = builder.Configuration.GetRequiredValue(builder.Environment, "REDIS_URL");
var masterKey = builder.Configuration.GetRequiredValue(builder.Environment, "PLATFORM_MASTER_KEY");
var jwtSigningKey = builder.Configuration.GetRequiredValue(builder.Environment, "JWT_SIGNING_KEY");
var platformBaseUrl = builder.Configuration.GetRequiredValue(builder.Environment, "PLATFORM_BASE_URL");

builder.Services.AddPlatformDataProtection(builder.Configuration, builder.Environment, "AxonVoiceAI.AgentConfig");

// ── Database ───────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AgentConfigDbContext>(options =>
    options.UseNpgsql(connectionString));

// ── Redis ──────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));

// ── Domain Services ────────────────────────────────────────────────────────────
builder.Services.AddSingleton(_ => new ApiKeyEncryptionService(masterKey));
builder.Services.AddScoped<IPasswordHasher<TenantUser>, PasswordHasher<TenantUser>>();
builder.Services.AddScoped<ConsoleAuthenticationService>();
builder.Services.AddSingleton(_ => new ConsoleTokenService(jwtSigningKey, platformBaseUrl));
builder.Services.AddSingleton(_ => new SessionTokenService(jwtSigningKey, platformBaseUrl));
builder.Services.AddScoped<IAvailabilityRepository, AvailabilityRepository>();
builder.Services.AddScoped<IBookingRepository, BookingRepository>();

// ── Authentication ─────────────────────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateIssuer = true,
            ValidIssuer = platformBaseUrl,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PlatformAuthorizationPolicyNames.ConsoleAccess, policy =>
    {
        policy.RequireAuthenticatedUser();
        policy.RequireClaim(PlatformTokenClaims.TokenUse, PlatformTokenUses.Console);
    });
});
builder.Services.AddControllers();

var app = builder.Build();

// Apply EF Core migrations on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgentConfigDbContext>();
    await db.Database.MigrateAsync();
}

app.UseExceptionHandler(pipeline =>
{
    pipeline.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("ExceptionHandler");
        logger.LogError(exceptionFeature?.Error, "Unhandled exception on {Method} {Path}",
            context.Request.Method, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "An internal error occurred." });
    });
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();

