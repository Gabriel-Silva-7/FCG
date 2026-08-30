using FCG.Domain.Common;

namespace FCG.Domain.Identity;

public sealed class User
{
    public const int MaxNameLength = 120;
    public const int MaxPasswordHashLength = 256;

    private User(
        Guid id,
        string name,
        Email email,
        string passwordHash,
        UserRole role,
        bool isActive,
        DateTime createdAtUtc)
    {
        Id = id;
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        IsActive = isActive;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; }

    public Email Email { get; private set; }

    public string PasswordHash { get; private set; }

    public UserRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public static User Register(
        string name,
        Email email,
        string passwordHash,
        DateTime createdAtUtc) =>
        Create(name, email, passwordHash, UserRole.User, createdAtUtc);

    public static User RegisterAdministrator(
        string name,
        Email email,
        string passwordHash,
        DateTime createdAtUtc) =>
        Create(name, email, passwordHash, UserRole.Administrator, createdAtUtc);

    public void ChangeActiveStatus(bool isActive) => IsActive = isActive;

    private static User Create(
        string name,
        Email email,
        string passwordHash,
        UserRole role,
        DateTime createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(email);
        ArgumentNullException.ThrowIfNull(passwordHash);

        var normalizedName = name.Trim();

        if (normalizedName.Length is 0 or > MaxNameLength)
        {
            throw new ArgumentException(
                $"Name must contain between 1 and {MaxNameLength} characters.",
                nameof(name));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash cannot be empty.", nameof(passwordHash));
        }

        if (passwordHash.Length > MaxPasswordHashLength)
        {
            throw new ArgumentException(
                $"Password hash cannot exceed {MaxPasswordHashLength} characters.",
                nameof(passwordHash));
        }

        DomainGuard.EnsureUtc(createdAtUtc, nameof(createdAtUtc));

        return new User(
            Guid.NewGuid(),
            normalizedName,
            email,
            passwordHash,
            role,
            true,
            createdAtUtc);
    }
}
