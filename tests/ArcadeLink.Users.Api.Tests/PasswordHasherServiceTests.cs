using ArcadeLink.Users.Api.Auth;
using ArcadeLink.Users.Api.Domain;
using Xunit;

namespace ArcadeLink.Users.Api.Tests;

public class PasswordHasherServiceTests
{
    [Fact]
    public void Hash_then_Verify_with_correct_password_succeeds()
    {
        var hasher = new PasswordHasherService();
        var user = new User { Id = Guid.NewGuid(), Email = "ada@example.com" };

        user.PasswordHash = hasher.Hash(user, "Senha1234");

        Assert.True(hasher.Verify(user, "Senha1234"));
    }

    [Fact]
    public void Verify_with_wrong_password_fails()
    {
        var hasher = new PasswordHasherService();
        var user = new User { Id = Guid.NewGuid(), Email = "ada@example.com" };

        user.PasswordHash = hasher.Hash(user, "Senha1234");

        Assert.False(hasher.Verify(user, "wrong-password"));
    }
}
