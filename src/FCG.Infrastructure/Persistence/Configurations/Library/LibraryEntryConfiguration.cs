using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Domain.Library;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Infrastructure.Persistence.Configurations.Library;

public sealed class LibraryEntryConfiguration : IEntityTypeConfiguration<LibraryEntry>
{
    public void Configure(EntityTypeBuilder<LibraryEntry> builder)
    {
        builder.ToTable(
            "LibraryEntries",
            table => table.HasCheckConstraint(
                "CK_LibraryEntries_AcquisitionPrice_NonNegative",
                "\"AcquisitionPrice\" >= 0"));

        builder.HasKey(entry => new { entry.UserId, entry.GameId })
            .HasName("PK_LibraryEntries");

        builder.Property(entry => entry.UserId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(entry => entry.GameId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(entry => entry.AcquiredAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(entry => entry.AcquisitionPrice)
            .HasColumnType("numeric(18,2)")
            .HasPrecision(18, LibraryEntry.PriceScale)
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(entry => entry.UserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LibraryEntries_Users_UserId");

        builder.HasOne<Game>()
            .WithMany()
            .HasForeignKey(entry => entry.GameId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_LibraryEntries_Games_GameId");

        builder.HasIndex(entry => entry.GameId)
            .HasDatabaseName("IX_LibraryEntries_GameId");
    }
}
