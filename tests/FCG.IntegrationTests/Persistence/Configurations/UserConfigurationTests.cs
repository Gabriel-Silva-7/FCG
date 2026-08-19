using FCG.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FCG.IntegrationTests.Persistence.Configurations;

public sealed class UserConfigurationTests
{
    [Fact]
    public void Model_ConfiguresUserTableAndStableKeys()
    {
        var entity = FcgDbContextModel.Entity<User>();
        var primaryKey = entity.FindPrimaryKey();
        var emailIndex = entity.GetIndexes().Single();

        Assert.Equal("Users", entity.GetTableName());
        Assert.Equal("PK_Users", primaryKey?.GetName());
        Assert.Equal(new[] { nameof(User.Id) }, primaryKey?.Properties.Select(property => property.Name));
        Assert.Equal("UX_Users_Email", emailIndex.GetDatabaseName());
        Assert.Equal(new[] { nameof(User.Email) }, emailIndex.Properties.Select(property => property.Name));
        Assert.True(emailIndex.IsUnique);
    }

    [Fact]
    public void Model_ConfiguresUserScalarProperties()
    {
        var entity = FcgDbContextModel.Entity<User>();
        var id = entity.FindProperty(nameof(User.Id));
        var name = entity.FindProperty(nameof(User.Name));
        var email = entity.FindProperty(nameof(User.Email));
        var passwordHash = entity.FindProperty(nameof(User.PasswordHash));
        var role = entity.FindProperty(nameof(User.Role));
        var isActive = entity.FindProperty(nameof(User.IsActive));
        var createdAtUtc = entity.FindProperty(nameof(User.CreatedAtUtc));

        Assert.Equal(
            new[]
            {
                nameof(User.CreatedAtUtc),
                nameof(User.Email),
                nameof(User.Id),
                nameof(User.IsActive),
                nameof(User.Name),
                nameof(User.PasswordHash),
                nameof(User.Role),
                "xmin",
            },
            entity.GetProperties().Select(property => property.Name).Order());

        Assert.Equal("uuid", id?.GetColumnType());
        Assert.False(id?.IsNullable);

        Assert.Equal($"character varying({User.MaxNameLength})", name?.GetColumnType());
        Assert.Equal(User.MaxNameLength, name?.GetMaxLength());
        Assert.False(name?.IsNullable);

        Assert.Equal($"character varying({Email.MaxLength})", email?.GetColumnType());
        Assert.Equal(Email.MaxLength, email?.GetMaxLength());
        Assert.False(email?.IsNullable);

        Assert.Equal(
            $"character varying({User.MaxPasswordHashLength})",
            passwordHash?.GetColumnType());
        Assert.Equal(User.MaxPasswordHashLength, passwordHash?.GetMaxLength());
        Assert.False(passwordHash?.IsNullable);

        Assert.Equal("character varying(20)", role?.GetColumnType());
        Assert.Equal(20, role?.GetMaxLength());
        Assert.False(role?.IsNullable);

        Assert.Equal("boolean", isActive?.GetColumnType());
        Assert.Equal(true, isActive?.GetDefaultValue());
        Assert.False(isActive?.IsNullable);

        Assert.Equal("timestamp with time zone", createdAtUtc?.GetColumnType());
        Assert.False(createdAtUtc?.IsNullable);
    }

    [Fact]
    public void Model_ConvertsEmailAndRoleToStrings()
    {
        var entity = FcgDbContextModel.Entity<User>();
        var emailConverter = entity.FindProperty(nameof(User.Email))?.GetValueConverter();
        var roleConverter = entity.FindProperty(nameof(User.Role))?.GetTypeMapping().Converter;
        var email = Email.Create("user@example.com");

        Assert.NotNull(emailConverter);
        Assert.Equal(email.Value, emailConverter.ConvertToProvider(email));
        Assert.Equal(email, emailConverter.ConvertFromProvider(email.Value));

        Assert.NotNull(roleConverter);
        Assert.Equal("Administrator", roleConverter.ConvertToProvider(UserRole.Administrator));
        Assert.Equal(UserRole.User, roleConverter.ConvertFromProvider("User"));
    }

    [Fact]
    public void Model_ConfiguresRoleCheckAndXminConcurrencyToken()
    {
        var entity = FcgDbContextModel.Entity<User>();
        var roleCheck = entity.GetCheckConstraints().Single(
            constraint => constraint.Name == "CK_Users_Role");
        var xmin = entity.FindProperty("xmin");

        Assert.Equal("\"Role\" IN ('User', 'Administrator')", roleCheck.Sql);

        Assert.NotNull(xmin);
        Assert.Equal(typeof(uint), xmin.ClrType);
        Assert.Equal("xid", xmin.GetColumnType());
        Assert.True(xmin.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, xmin.ValueGenerated);
    }
}
