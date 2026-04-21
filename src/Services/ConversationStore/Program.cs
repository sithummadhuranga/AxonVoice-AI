using AxonVoiceAI.ConversationStore.Data;
using AxonVoiceAI.ConversationStore.Services;
using AxonVoiceAI.Shared.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetRequiredValue(builder.Environment, "POSTGRES_CONNECTION_STRING", "POSTGRES_URL");
var jwtSigningKey = builder.Configuration.GetRequiredValue(builder.Environment, "JWT_SIGNING_KEY");
var platformBaseUrl = builder.Configuration.GetRequiredValue(builder.Environment, "PLATFORM_BASE_URL");
var ollamaUrl = builder.Configuration.GetRequiredValue(builder.Environment, "OLLAMA_URL");
var ollamaModel = builder.Configuration.GetValueOrDefault("llama3.2", "OLLAMA_SUMMARY_MODEL", "OLLAMA_COMPLETION_MODEL");

builder.Services.AddPlatformDataProtection(builder.Configuration, builder.Environment, "AxonVoiceAI.ConversationStore");

builder.Services.AddDbContext<ConversationDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddHttpClient("ollama", client =>
{
    client.BaseAddress = new Uri(ollamaUrl);
    client.Timeout = TimeSpan.FromMinutes(2);
});

builder.Services.AddHttpClient("webhook", client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
});

builder.Services.AddScoped<SummaryService>(sp =>
{
    var factory = sp.GetRequiredService<IHttpClientFactory>();
    var client = factory.CreateClient("ollama");
    var logger = sp.GetRequiredService<ILogger<SummaryService>>();
    return new SummaryService(client, ollamaModel, logger);
});

builder.Services.AddHostedService<WebhookDispatcher>();

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ConversationDbContext>();
    await db.Database.MigrateAsync();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
