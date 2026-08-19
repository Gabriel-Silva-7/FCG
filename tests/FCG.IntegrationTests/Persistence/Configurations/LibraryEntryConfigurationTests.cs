using FCG.Domain.Catalog;
using FCG.Domain.Identity;
using FCG.Domain.Library;
using Microsoft.EntityFrameworkCore;

namespace FCG.IntegrationTests.Persistence.Configurations;

public sealed class LibraryEntryConfigurationTests
{
    [Fact]
    public void Model_ConfiguresLibraryEntryCompositeKeyAndScalarProperties()
    {
        var entity = FcgDbContextModel.Entity<LibraryEntry>();
        var primaryKey = entity.FindPrimaryKey();
        var userId = entity.FindProperty(nameof(LibraryEntry.UserId));
        var gameId = entity.FindProperty(nameof(LibraryEntry.GameId));
        var acquiredAtUtc = entity.FindProperty(nameof(LibraryEntry.AcquiredAtUtc));
        var acquisitionPrice = entity.FindProperty(nameof(LibraryEntry.AcquisitionPrice));

        Assert.Equal("LibraryEntries", entity.GetTableName());
        Assert.Equal("PK_LibraryEntries", primaryKey?.GetName());
        Assert.Equal(
            new[] { nameof(LibraryEntry.UserId), nameof(LibraryEntry.GameId) },
            primaryKey?.Properties.Select(property => property.Name));
        Assert.Equal(
            new[]
            {
                nameof(LibraryEntry.AcquiredAtUtc),
                nameof(LibraryEntry.AcquisitionPrice),
                nameof(LibraryEntry.GameId),
                nameof(LibraryEntry.UserId),
            },
            entity.GetProperties().Select(property => property.Name).Order());

        Assert.Equal("uuid", userId?.GetColumnType());
        Assert.False(userId?.IsNullable);
        Assert.Equal("uuid", gameId?.GetColumnType());
        Assert.False(gameId?.IsNullable);

        Assert.Equal("timestamp with time zone", acquiredAtUtc?.GetColumnType());
        Assert.False(acquiredAtUtc?.IsNullable);

        Assert.Equal("numeric(18,2)", acquisitionPrice?.GetColumnType());
        Assert.Equal(18, acquisitionPrice?.GetPrecision());
        Assert.Equal(LibraryEntry.PriceScale, acquisitionPrice?.GetScale());
        Assert.False(acquisitionPrice?.IsNullable);
    }

    [Fact]
    public void Model_ConfiguresLibraryEntryPriceCheck()
    {
        var constraint = FcgDbContextModel.Entity<LibraryEntry>()
            .GetCheckConstraints()
            .Single();

        Assert.Equal("CK_LibraryEntries_AcquisitionPrice_NonNegative", constraint.Name);
        Assert.Equal("\"AcquisitionPrice\" >= 0", constraint.Sql);
    }

    [Fact]
    public void Model_ConfiguresLibraryEntryRelationshipsAndOnlyNecessarySecondaryIndex()
    {
        var entity = FcgDbContextModel.Entity<LibraryEntry>();
        var userForeignKey = entity.GetForeignKeys().Single(
            foreignKey => foreignKey.Properties.Single().Name == nameof(LibraryEntry.UserId));
        var gameForeignKey = entity.GetForeignKeys().Single(
            foreignKey => foreignKey.Properties.Single().Name == nameof(LibraryEntry.GameId));
        var secondaryIndex = entity.GetIndexes().Single();

        Assert.Equal(typeof(User), userForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, userForeignKey.DeleteBehavior);
        Assert.Equal("FK_LibraryEntries_Users_UserId", userForeignKey.GetConstraintName());

        Assert.Equal(typeof(Game), gameForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, gameForeignKey.DeleteBehavior);
        Assert.Equal("FK_LibraryEntries_Games_GameId", gameForeignKey.GetConstraintName());

        Assert.Equal("IX_LibraryEntries_GameId", secondaryIndex.GetDatabaseName());
        Assert.Equal(
            new[] { nameof(LibraryEntry.GameId) },
            secondaryIndex.Properties.Select(property => property.Name));
    }
}
