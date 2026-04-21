using Microsoft.AspNetCore.Authentication.JwtBearer;
using AxonVoiceAI.Shared.Configuration;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var jwtSigningKey = builder.Configuration.GetRequiredValue(builder.Environment, "JWT_SIGNING_KEY");
var platformBaseUrl = builder.Configuration.GetRequiredValue(builder.Environment, "PLATFORM_BASE_URL");
var redisUrl = builder.Configuration.GetRequiredValue(builder.Environment, "REDIS_URL");

builder.Services.AddPlatformDataProtection(builder.Configuration, builder.Environment, "AxonVoiceAI.Gateway");

builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(redisUrl));

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

        // Allow JWT via WebSocket query parameter for session upgrades.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["token"].FirstOrDefault();
                if (!string.IsNullOrEmpty(token))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseWebSockets();
app.UseAuthentication();
app.UseAuthorization();

// Rate limiting check before proxying — reads counters set by AgentConfig.
app.Use(async (context, next) =>
{
    var tenantIdClaim = context.User.FindFirst("tenant_id")?.Value;
    if (tenantIdClaim is not null)
    {
        var redis = context.RequestServices.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();

        var dailyKey = $"rate:daily:{tenantIdClaim}:{DateTime.UtcNow:yyyyMMdd}";
        var concurrentKey = $"rate:concurrent:{tenantIdClaim}";

        var dailyCount = (long?)await db.StringGetAsync(dailyKey) ?? 0;
        var concurrentCount = (long?)await db.StringGetAsync(concurrentKey) ?? 0;

        // Limits are checked here but enforced by AgentConfig — gateway blocks if the
        // counters have already been exceeded (defensive check, not the authoritative one).
        const long DailyLimit = 1000;
        const long ConcurrentLimit = 10;

        if (dailyCount >= DailyLimit || concurrentCount >= ConcurrentLimit)
        {
            context.Response.StatusCode = 429;
            context.Response.Headers.RetryAfter = "60";
            return;
        }
    }

    await next(context);
});

app.MapReverseProxy();

await app.RunAsync();
