using System;
using AiDesktopAssistant.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

#nullable disable

namespace AiDesktopAssistant.Infrastructure.Migrations;

[DbContext(typeof(AssistantDbContext))]
partial class AssistantDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.8");

        modelBuilder.Entity("AiDesktopAssistant.Core.Entities.ChatMessage", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT");

            b.Property<string>("Content")
                .IsRequired()
                .HasColumnType("TEXT");

            b.Property<Guid>("ConversationId")
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("ReasoningContent")
                .HasColumnType("TEXT");

            b.Property<string>("Role")
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.HasIndex("ConversationId", "CreatedAt");

            b.ToTable("Messages");
        });

        modelBuilder.Entity("AiDesktopAssistant.Core.Entities.Conversation", b =>
        {
            b.Property<Guid>("Id")
                .ValueGeneratedOnAdd()
                .HasColumnType("TEXT");

            b.Property<DateTime>("CreatedAt")
                .HasColumnType("TEXT");

            b.Property<string>("Title")
                .IsRequired()
                .HasMaxLength(200)
                .HasColumnType("TEXT");

            b.Property<DateTime>("UpdatedAt")
                .HasColumnType("TEXT");

            b.HasKey("Id");

            b.ToTable("Conversations");
        });

        modelBuilder.Entity("AiDesktopAssistant.Core.Entities.ChatMessage", b =>
        {
            b.HasOne("AiDesktopAssistant.Core.Entities.Conversation")
                .WithMany("Messages")
                .HasForeignKey("ConversationId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("AiDesktopAssistant.Core.Entities.Conversation", b =>
        {
            b.Navigation("Messages");
        });
    }
}
