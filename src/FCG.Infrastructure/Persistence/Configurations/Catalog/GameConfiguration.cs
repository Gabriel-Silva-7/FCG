using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Infrastructure.Persistence.Configurations.Catalog;

public sealed class GameConfiguration : IEntityTypeConfiguration<Game>
{
    public void Configure(EntityTypeBuilder<Game> builder)
    {
        builder.ToTable(
            "Games",
            table => table.HasCheckConstraint(
                "CK_Games_BasePrice_NonNegative",
                "\"BasePrice\" >= 0"));

        builder.HasKey(game => game.Id)
            .HasName("PK_Games");

        builder.Property(game => game.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(game => game.Title)
            .HasColumnType($"character varying({Game.MaxTitleLength})")
            .HasMaxLength(Game.MaxTitleLength)
            .IsRequired();

        builder.Property(game => game.Description)
            .HasColumnType($"character varying({Game.MaxDescriptionLength})")
            .HasMaxLength(Game.MaxDescriptionLength)
            .IsRequired(false);

        builder.Property(game => game.BasePrice)
            .HasColumnType("numeric(18,2)")
            .HasPrecision(18, Game.BasePriceScale)
            .IsRequired();

        builder.Property(game => game.IsActive)
            .HasColumnType("boolean")
            .IsRequired();

        builder.Property(game => game.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(game => game.CreatedByUserId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(game => game.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Games_Users_CreatedByUserId");

        builder.HasIndex(game => game.CreatedByUserId)
            .HasDatabaseName("IX_Games_CreatedByUserId");
    }
}
