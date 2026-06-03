using AiDesktopAssistant.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace AiDesktopAssistant.Infrastructure.Persistence;

public sealed class AssistantDbContext(DbContextOptions<AssistantDbContext> options) : DbContext(options)
{
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Conversation>(builder =>
        {
            builder.ToTable("Conversations");
            builder.HasKey(conversation => conversation.Id);
            builder.Property(conversation => conversation.Title).IsRequired().HasMaxLength(200);
            builder.Property(conversation => conversation.CreatedAt).IsRequired();
            builder.Property(conversation => conversation.UpdatedAt).IsRequired();
            builder.HasMany(conversation => conversation.Messages)
                .WithOne()
                .HasForeignKey(message => message.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ChatMessage>(builder =>
        {
            builder.ToTable("Messages");
            builder.HasKey(message => message.Id);
            builder.Property(message => message.Role).HasConversion<string>().IsRequired().HasMaxLength(32);
            builder.Property(message => message.Content).IsRequired();
            builder.Property(message => message.ReasoningContent);
            builder.Property(message => message.CreatedAt).IsRequired();
            builder.HasIndex(message => new { message.ConversationId, message.CreatedAt });
        });
    }
}
