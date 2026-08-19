using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FCG.IntegrationTests.Persistence.Configurations;

public sealed class GameConfigurationTests
{
    [Fact]
    public void Model_ConfiguresGameTableAndScalarProperties()
    {
        var entity = FcgDbContextModel.Entity<Game>();
        var id = entity.FindProperty(nameof(Game.Id));
        var title = entity.FindProperty(nameof(Game.Title));
        var description = entity.FindProperty(nameof(Game.Description));
        var basePrice = entity.FindProperty(nameof(Game.BasePrice));
        var isActive = entity.FindProperty(nameof(Game.IsActive));
        var createdAtUtc = entity.FindProperty(nameof(Game.CreatedAtUtc));
        var createdByUserId = entity.FindProperty(nameof(Game.CreatedByUserId));

        Assert.Equal("Games", entity.GetTableName());
        Assert.Equal("PK_Games", entity.FindPrimaryKey()?.GetName());
        Assert.Equal(
            new[]
            {
                nameof(Game.BasePrice),
                nameof(Game.CreatedAtUtc),
                nameof(Game.CreatedByUserId),
                nameof(Game.Description),
                nameof(Game.Id),
                nameof(Game.IsActive),
                nameof(Game.Title),
            },
            entity.GetProperties().Select(property => property.Name).Order());

        Assert.Equal("uuid", id?.GetColumnType());
        Assert.False(id?.IsNullable);

        Assert.Equal($"character varying({Game.MaxTitleLength})", title?.GetColumnType());
        Assert.Equal(Game.MaxTitleLength, title?.GetMaxLength());
        Assert.False(title?.IsNullable);

        Assert.Equal($"character varying({Game.MaxDescriptionLength})", description?.GetColumnType());
        Assert.Equal(Game.MaxDescriptionLength, description?.GetMaxLength());
        Assert.True(description?.IsNullable);

        Assert.Equal("numeric(18,2)", basePrice?.GetColumnType());
        Assert.Equal(18, basePrice?.GetPrecision());
        Assert.Equal(Game.BasePriceScale, basePrice?.GetScale());
        Assert.False(basePrice?.IsNullable);

        Assert.Equal("boolean", isActive?.GetColumnType());
        Assert.Null(isActive?.FindAnnotation(RelationalAnnotationNames.DefaultValue));
        Assert.False(isActive?.IsNullable);

        Assert.Equal("timestamp with time zone", createdAtUtc?.GetColumnType());
        Assert.False(createdAtUtc?.IsNullable);

        Assert.Equal("uuid", createdByUserId?.GetColumnType());
        Assert.False(createdByUserId?.IsNullable);
    }

    [Fact]
    public void Model_ConfiguresGameConstraintAndCreatorRelationship()
    {
        var entity = FcgDbContextModel.Entity<Game>();
        var basePriceCheck = entity.GetCheckConstraints().Single(
            constraint => constraint.Name == "CK_Games_BasePrice_NonNegative");
        var creatorForeignKey = entity.GetForeignKeys().Single();
        var creatorIndex = entity.GetIndexes().Single(
            index => index.Properties.Single().Name == nameof(Game.CreatedByUserId));

        Assert.Single(entity.GetCheckConstraints());
        Assert.Equal("\"BasePrice\" >= 0", basePriceCheck.Sql);
        Assert.Equal(typeof(User), creatorForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, creatorForeignKey.DeleteBehavior);
        Assert.Equal("FK_Games_Users_CreatedByUserId", creatorForeignKey.GetConstraintName());
        Assert.Equal("IX_Games_CreatedByUserId", creatorIndex.GetDatabaseName());
    }

    [Fact]
    public void Model_DoesNotMakeGameTitleUniqueOrFilterInactiveGames()
    {
        var entity = FcgDbContextModel.Entity<Game>();

        Assert.DoesNotContain(
            entity.GetIndexes(),
            index => index.Properties.Any(property => property.Name == nameof(Game.Title)));
        Assert.Null(entity.GetQueryFilter());
    }
}
