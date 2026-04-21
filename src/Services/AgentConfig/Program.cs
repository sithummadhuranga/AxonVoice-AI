using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Repositories;
using AxonVoiceAI.AgentConfig.Services;
using AxonVoiceAI.Shared.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────────
var connectionString = builder.Configuration["POSTGRES_CONNECTION_STRING"]
    ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
var redisConnectionString = builder.Configuration["REDIS_URL"]
    ?? throw new InvalidOperationException("REDIS_URL is required.");
var masterKey = builder.Configuration["PLATFORM_MASTER_KEY"]
    ?? throw new InvalidOperationException("PLATFORM_MASTER_KEY is required.");
var jwtSigningKey = builder.Configuration["JWT_SIGNING_KEY"]
    ?? throw new InvalidOperationException("JWT_SIGNING_KEY is required.");
var platformBaseUrl = builder.Configuration["PLATFORM_BASE_URL"]
    ?? throw new InvalidOperationException("PLATFORM_BASE_URL is required.");

// ── Database ───────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AgentConfigDbContext>(options =>
    options.UseNpgsql(connectionString));

// ── Redis ──────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisConnectionString));

// ── Domain Services ────────────────────────────────────────────────────────────
builder.Services.AddSingleton(_ => new ApiKeyEncryptionService(masterKey));
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

builder.Services.AddAuthorization();
builder.Services.AddControllers();

var app = builder.Build();

// Apply EF Core migrations on startup.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgentConfigDbContext>();
    await db.Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();

