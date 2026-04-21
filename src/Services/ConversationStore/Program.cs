using AxonVoiceAI.ConversationStore.Data;
using AxonVoiceAI.ConversationStore.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["POSTGRES_CONNECTION_STRING"]
    ?? throw new InvalidOperationException("POSTGRES_CONNECTION_STRING is required.");
var jwtSigningKey = builder.Configuration["JWT_SIGNING_KEY"]
    ?? throw new InvalidOperationException("JWT_SIGNING_KEY is required.");
var platformBaseUrl = builder.Configuration["PLATFORM_BASE_URL"]
    ?? throw new InvalidOperationException("PLATFORM_BASE_URL is required.");
var ollamaUrl = builder.Configuration["OLLAMA_URL"]
    ?? throw new InvalidOperationException("OLLAMA_URL is required.");
var ollamaModel = builder.Configuration["OLLAMA_SUMMARY_MODEL"] ?? "llama3.2";

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
