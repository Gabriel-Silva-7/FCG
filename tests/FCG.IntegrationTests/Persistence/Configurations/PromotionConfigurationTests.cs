using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace FCG.IntegrationTests.Persistence.Configurations;

public sealed class PromotionConfigurationTests
{
    [Fact]
    public void Model_ConfiguresPromotionTableAndScalarProperties()
    {
        var entity = FcgDbContextModel.Entity<Promotion>();
        var id = entity.FindProperty(nameof(Promotion.Id));
        var gameId = entity.FindProperty(nameof(Promotion.GameId));
        var discount = entity.FindProperty(nameof(Promotion.DiscountPercentage));
        var startsAtUtc = entity.FindProperty(nameof(Promotion.StartsAtUtc));
        var endsAtUtc = entity.FindProperty(nameof(Promotion.EndsAtUtc));
        var createdByUserId = entity.FindProperty(nameof(Promotion.CreatedByUserId));

        Assert.Equal("Promotions", entity.GetTableName());
        Assert.Equal("PK_Promotions", entity.FindPrimaryKey()?.GetName());
        Assert.Equal(
            new[]
            {
                nameof(Promotion.CreatedByUserId),
                nameof(Promotion.DiscountPercentage),
                nameof(Promotion.EndsAtUtc),
                nameof(Promotion.GameId),
                nameof(Promotion.Id),
                nameof(Promotion.StartsAtUtc),
            },
            entity.GetProperties().Select(property => property.Name).Order());

        Assert.Equal("uuid", id?.GetColumnType());
        Assert.False(id?.IsNullable);
        Assert.Equal("uuid", gameId?.GetColumnType());
        Assert.False(gameId?.IsNullable);

        Assert.Equal("numeric(5,2)", discount?.GetColumnType());
        Assert.Equal(5, discount?.GetPrecision());
        Assert.Equal(Promotion.DiscountPercentageScale, discount?.GetScale());
        Assert.False(discount?.IsNullable);

        Assert.Equal("timestamp with time zone", startsAtUtc?.GetColumnType());
        Assert.False(startsAtUtc?.IsNullable);
        Assert.Equal("timestamp with time zone", endsAtUtc?.GetColumnType());
        Assert.False(endsAtUtc?.IsNullable);

        Assert.Equal("uuid", createdByUserId?.GetColumnType());
        Assert.False(createdByUserId?.IsNullable);
    }

    [Fact]
    public void Model_ConfiguresPromotionChecks()
    {
        var checks = FcgDbContextModel.Entity<Promotion>()
            .GetCheckConstraints()
            .ToDictionary(constraint => constraint.Name!, constraint => constraint.Sql);

        Assert.Equal(2, checks.Count);
        Assert.Equal(
            "\"DiscountPercentage\" > 0 AND \"DiscountPercentage\" <= 100",
            checks["CK_Promotions_DiscountPercentage_Range"]);
        Assert.Equal(
            "\"EndsAtUtc\" > \"StartsAtUtc\"",
            checks["CK_Promotions_DateRange"]);
    }

    [Fact]
    public void Model_ConfiguresPromotionRelationshipsAndIndexes()
    {
        var entity = FcgDbContextModel.Entity<Promotion>();
        var gameForeignKey = entity.GetForeignKeys().Single(
            foreignKey => foreignKey.Properties.Single().Name == nameof(Promotion.GameId));
        var creatorForeignKey = entity.GetForeignKeys().Single(
            foreignKey => foreignKey.Properties.Single().Name == nameof(Promotion.CreatedByUserId));
        var indexes = entity.GetIndexes()
            .ToDictionary(index => index.GetDatabaseName()!);

        Assert.Equal(typeof(Game), gameForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, gameForeignKey.DeleteBehavior);
        Assert.Equal("FK_Promotions_Games_GameId", gameForeignKey.GetConstraintName());

        Assert.Equal(typeof(User), creatorForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, creatorForeignKey.DeleteBehavior);
        Assert.Equal("FK_Promotions_Users_CreatedByUserId", creatorForeignKey.GetConstraintName());

        Assert.Equal(2, indexes.Count);
        Assert.Equal(
            new[]
            {
                nameof(Promotion.GameId),
                nameof(Promotion.StartsAtUtc),
                nameof(Promotion.EndsAtUtc),
            },
            indexes["IX_Promotions_GameId_StartsAtUtc_EndsAtUtc"]
                .Properties
                .Select(property => property.Name));
        Assert.Equal(
            new[] { nameof(Promotion.CreatedByUserId) },
            indexes["IX_Promotions_CreatedByUserId"]
                .Properties
                .Select(property => property.Name));
    }
}
