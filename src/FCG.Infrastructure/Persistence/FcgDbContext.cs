using Microsoft.EntityFrameworkCore;

namespace FCG.Infrastructure.Persistence;

public sealed class FcgDbContext(DbContextOptions<FcgDbContext> options) : DbContext(options)
{
}
