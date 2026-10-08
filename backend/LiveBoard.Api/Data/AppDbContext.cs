using LiveBoard.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace LiveBoard.Api.Data;

/// <summary>
/// Kept free of provider-specific column types so the same model runs on PostgreSQL in production and on
/// SQLite in the integration tests.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardMember> BoardMembers => Set<BoardMember>();
    public DbSet<BoardColumn> Columns => Set<BoardColumn>();
    public DbSet<Card> Cards => Set<Card>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("users");
            e.HasKey(u => u.Id);
            e.Property(u => u.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.DisplayName).HasMaxLength(40).IsRequired();
            e.Property(u => u.PasswordHash).IsRequired();
        });

        modelBuilder.Entity<Board>(e =>
        {
            e.ToTable("boards");
            e.HasKey(b => b.Id);
            e.Property(b => b.Title).HasMaxLength(120).IsRequired();
            e.Property(b => b.ShareToken).HasMaxLength(64).IsRequired();
            e.HasIndex(b => b.ShareToken).IsUnique();
            e.HasMany(b => b.Columns).WithOne().HasForeignKey(c => c.BoardId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(b => b.Members).WithOne().HasForeignKey(m => m.BoardId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BoardMember>(e =>
        {
            e.ToTable("board_members");
            e.HasKey(m => new { m.BoardId, m.UserId });
            e.Property(m => m.Role).HasConversion<string>().HasMaxLength(16);
            e.HasOne(m => m.User).WithMany().HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(m => m.UserId);
        });

        modelBuilder.Entity<BoardColumn>(e =>
        {
            e.ToTable("board_columns");
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).HasMaxLength(120).IsRequired();
            e.HasMany(c => c.Cards).WithOne().HasForeignKey(c => c.ColumnId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.BoardId, c.Position });
        });

        modelBuilder.Entity<Card>(e =>
        {
            e.ToTable("cards");
            e.HasKey(c => c.Id);
            e.Property(c => c.Title).HasMaxLength(200).IsRequired();
            e.Property(c => c.Description).HasMaxLength(5000).IsRequired();
            e.Property(c => c.Color).HasMaxLength(20);
            // Plain indexed column (no FK): cards are already removed with their column, which cascades from
            // the board, and a second cascade path from boards would be redundant.
            e.HasIndex(c => c.BoardId);
            e.HasIndex(c => new { c.ColumnId, c.Position });
        });
    }
}

/// <summary>Used by `dotnet ef` so migrations can be created without real secrets configured.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=liveboard;Username=postgres;Password=postgres")
            .Options);
}
