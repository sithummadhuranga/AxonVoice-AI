using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AxonVoiceAI.AgentConfig.Data;

/// <summary>
/// Provides a DbContext instance for EF Core design-time tooling (migrations)
/// without requiring the full application to start.
/// </summary>
public sealed class AgentConfigDbContextFactory : IDesignTimeDbContextFactory<AgentConfigDbContext>
{
    public AgentConfigDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AgentConfigDbContext>();
        // Use a placeholder connection string at design time.
        // The real value is injected at runtime from the POSTGRES_CONNECTION_STRING environment variable.
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=axonvoice_agentconfig;Username=postgres;Password=postgres",
            npgsql => npgsql.MigrationsAssembly(typeof(AgentConfigDbContext).Assembly.FullName));
        return new AgentConfigDbContext(optionsBuilder.Options);
    }
}
