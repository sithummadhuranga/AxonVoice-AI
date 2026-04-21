using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AxonVoiceAI.ConversationStore.Data;

/// <summary>
/// Provides a DbContext instance for EF Core design-time tooling (migrations)
/// without requiring the full application to start.
/// </summary>
public sealed class ConversationDbContextFactory : IDesignTimeDbContextFactory<ConversationDbContext>
{
    public ConversationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ConversationDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=axonvoice_conversations;Username=postgres;Password=postgres",
            npgsql => npgsql.MigrationsAssembly(typeof(ConversationDbContext).Assembly.FullName));
        return new ConversationDbContext(optionsBuilder.Options);
    }
}
