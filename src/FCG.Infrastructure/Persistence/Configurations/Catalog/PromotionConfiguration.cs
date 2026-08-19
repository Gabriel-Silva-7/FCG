using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Infrastructure.Persistence.Configurations.Catalog;

public sealed class PromotionConfiguration : IEntityTypeConfiguration<Promotion>
{
    public void Configure(EntityTypeBuilder<Promotion> builder)
    {
        builder.ToTable(
            "Promotions",
            table =>
            {
                table.HasCheckConstraint(
                    "CK_Promotions_DiscountPercentage_Range",
                    "\"DiscountPercentage\" > 0 AND \"DiscountPercentage\" <= 100");
                table.HasCheckConstraint(
                    "CK_Promotions_DateRange",
                    "\"EndsAtUtc\" > \"StartsAtUtc\"");
            });

        builder.HasKey(promotion => promotion.Id)
            .HasName("PK_Promotions");

        builder.Property(promotion => promotion.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(promotion => promotion.GameId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.Property(promotion => promotion.DiscountPercentage)
            .HasColumnType("numeric(5,2)")
            .HasPrecision(5, Promotion.DiscountPercentageScale)
            .IsRequired();

        builder.Property(promotion => promotion.StartsAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(promotion => promotion.EndsAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(promotion => promotion.CreatedByUserId)
            .HasColumnType("uuid")
            .IsRequired();

        builder.HasIndex(promotion => new
        {
            promotion.GameId,
            promotion.StartsAtUtc,
            promotion.EndsAtUtc,
        })
            .HasDatabaseName("IX_Promotions_GameId_StartsAtUtc_EndsAtUtc");

        builder.HasIndex(promotion => promotion.CreatedByUserId)
            .HasDatabaseName("IX_Promotions_CreatedByUserId");

        builder.HasOne<Game>()
            .WithMany()
            .HasForeignKey(promotion => promotion.GameId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Promotions_Games_GameId");

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(promotion => promotion.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_Promotions_Users_CreatedByUserId");
    }
}
