using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Domain.Library;
using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Persistence;

public sealed class FcgDbContext(DbContextOptions<FcgDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Game> Games => Set<Game>();

    public DbSet<Promotion> Promotions => Set<Promotion>();

    public DbSet<LibraryEntry> LibraryEntries => Set<LibraryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FcgDbContext).Assembly);
    }
}
