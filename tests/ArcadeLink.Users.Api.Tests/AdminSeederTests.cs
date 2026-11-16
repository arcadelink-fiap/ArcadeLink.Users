using ArcadeLink.Users.Api.Auth;
using ArcadeLink.Users.Api.Data;
using ArcadeLink.Users.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArcadeLink.Users.Api.Tests;

public class AdminSeederTests
{
    private static UsersDbContext NewDb() =>
        new(new DbContextOptionsBuilder<UsersDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AdminSeedOptions Options(string? email = "admin@arcadelink.com", string? password = "Admin1234") =>
        new() { Email = email, Password = password };

    [Fact]
    public async Task Creates_admin_when_absent()
    {
        using var db = NewDb();
        var hasher = new PasswordHasherService();

        var created = await AdminSeeder.SeedAsync(db, hasher, Options());

        Assert.True(created);
        var user = Assert.Single(db.Users);
        Assert.Equal(UserRole.Admin, user.Role);
        Assert.Equal("admin@arcadelink.com", user.Email); // /auth/register stores the e-mail verbatim
        Assert.True(hasher.Verify(user, "Admin1234"));
    }

    [Fact]
    public async Task Is_idempotent()
    {
        using var db = NewDb();
        var hasher = new PasswordHasherService();

        Assert.True(await AdminSeeder.SeedAsync(db, hasher, Options()));
        Assert.False(await AdminSeeder.SeedAsync(db, hasher, Options()));
        Assert.Single(db.Users);
    }

    [Theory]
    [InlineData(null, "Admin1234")]
    [InlineData("", "Admin1234")]
    [InlineData("admin@arcadelink.com", null)]
    [InlineData("admin@arcadelink.com", "")]
    public async Task Skips_when_not_configured(string? email, string? password)
    {
        using var db = NewDb();

        var created = await AdminSeeder.SeedAsync(db, new PasswordHasherService(), Options(email, password));

        Assert.False(created);
        Assert.Empty(db.Users);
    }

    [Fact]
    public async Task Does_not_downgrade_or_touch_existing_user()
    {
        using var db = NewDb();
        var hasher = new PasswordHasherService();
        var existing = new User
        {
            Id = Guid.NewGuid(), Name = "Comum", Email = "admin@arcadelink.com",
            Role = UserRole.User, CreatedAtUtc = DateTime.UtcNow
        };
        existing.PasswordHash = hasher.Hash(existing, "Outra1234");
        db.Users.Add(existing);
        await db.SaveChangesAsync();
        var originalHash = existing.PasswordHash;

        var created = await AdminSeeder.SeedAsync(db, hasher, Options());

        Assert.False(created);
        var user = Assert.Single(db.Users);
        Assert.Equal(UserRole.User, user.Role);
        Assert.Equal(originalHash, user.PasswordHash);
    }
}
