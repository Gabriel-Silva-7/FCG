using FCG.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FCG.Infrastructure.Persistence.Configurations.Identity;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(
            "Users",
            table => table.HasCheckConstraint(
                "CK_Users_Role",
                "\"Role\" IN ('User', 'Administrator')"));

        builder.HasKey(user => user.Id)
            .HasName("PK_Users");

        builder.Property(user => user.Id)
            .HasColumnType("uuid")
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(user => user.Name)
            .HasColumnType($"character varying({User.MaxNameLength})")
            .HasMaxLength(User.MaxNameLength)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasConversion(
                email => email.Value,
                value => Email.Create(value))
            .HasColumnType($"character varying({Email.MaxLength})")
            .HasMaxLength(Email.MaxLength)
            .IsRequired();

        builder.Property(user => user.PasswordHash)
            .HasColumnType($"character varying({User.MaxPasswordHashLength})")
            .HasMaxLength(User.MaxPasswordHashLength)
            .IsRequired();

        builder.Property(user => user.Role)
            .HasConversion<string>()
            .HasColumnType("character varying(20)")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(user => user.IsActive)
            .HasColumnType("boolean")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(user => user.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property<uint>("xmin")
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .IsRowVersion();

        builder.HasIndex(user => user.Email)
            .IsUnique()
            .HasDatabaseName("UX_Users_Email");
    }
}
