using AiDesktopAssistant.Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace AiDesktopAssistant.Infrastructure.Persistence;

public class AssistantDbContext(DbContextOptions<AssistantDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>()
            .HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatMessage>()
            .Property(m => m.Role)
            .HasConversion<string>();
    }
}
