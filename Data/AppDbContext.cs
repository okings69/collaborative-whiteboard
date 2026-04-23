using CollaborativeBoard.Entities;
using Microsoft.EntityFrameworkCore;

namespace CollaborativeBoard.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardPage> BoardPages => Set<BoardPage>();
    public DbSet<DrawingElement> DrawingElements => Set<DrawingElement>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Participant>(entity =>
        {
            entity.ToTable("Participants");
            entity.HasKey(participant => participant.Id);
            entity.Property(participant => participant.Nickname).HasMaxLength(64).IsRequired();
            entity.Property(participant => participant.AccentColor).HasMaxLength(32).IsRequired();
            entity.Property(participant => participant.LastConnectionId).HasMaxLength(128);
            entity.HasIndex(participant => participant.Nickname).IsUnique();
        });

        modelBuilder.Entity<Board>(entity =>
        {
            entity.ToTable("Boards");
            entity.HasKey(board => board.Id);
            entity.Property(board => board.Name).HasMaxLength(120).IsRequired();
            entity.Property(board => board.ShareCode).HasMaxLength(24).IsRequired();
            entity.Property(board => board.AccentColor).HasMaxLength(32).IsRequired();
            entity.Property(board => board.CreatedByNickname).HasMaxLength(64).IsRequired();
            entity.HasIndex(board => board.ShareCode).IsUnique();
            entity.HasOne(board => board.OwnerParticipant)
                .WithMany(participant => participant.OwnedBoards)
                .HasForeignKey(board => board.OwnerParticipantId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasMany(board => board.Pages)
                .WithOne(page => page.Board)
                .HasForeignKey(page => page.BoardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BoardPage>(entity =>
        {
            entity.ToTable("BoardPages");
            entity.HasKey(page => page.Id);
            entity.Property(page => page.Title).HasMaxLength(120).IsRequired();
            entity.HasIndex(page => new { page.BoardId, page.SortOrder }).IsUnique();
            entity.HasMany(page => page.Elements)
                .WithOne(element => element.BoardPage)
                .HasForeignKey(element => element.BoardPageId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DrawingElement>(entity =>
        {
            entity.ToTable("DrawingElements");
            entity.HasKey(element => element.Id);
            entity.Property(element => element.ElementType).HasConversion<string>().HasMaxLength(24).IsRequired();
            entity.Property(element => element.StrokeColor).HasMaxLength(32).IsRequired();
            entity.Property(element => element.FillColor).HasMaxLength(32);
            entity.Property(element => element.TextContent).HasMaxLength(2000);
            entity.Property(element => element.PointsJson).HasColumnType("jsonb");
            entity.Property(element => element.MetadataJson).HasColumnType("jsonb");
            entity.Property(element => element.VersionToken).HasMaxLength(40);
            entity.Property(element => element.CreatedByNickname).HasMaxLength(64).IsRequired();
            entity.HasOne(element => element.CreatedByParticipant)
                .WithMany(participant => participant.AuthoredElements)
                .HasForeignKey(element => element.CreatedByParticipantId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(element => new { element.BoardPageId, element.CreatedAtUtc });
            entity.HasIndex(element => new { element.BoardPageId, element.LayerOrder });
        });
    }
}
