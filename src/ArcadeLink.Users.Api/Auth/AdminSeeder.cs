using ArcadeLink.Users.Api.Data;
using ArcadeLink.Users.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace ArcadeLink.Users.Api.Auth;

public static class AdminSeeder
{
    /// <summary>Cria o Admin inicial se configurado e ainda inexistente. Retorna true só quando cria.</summary>
    public static async Task<bool> SeedAsync(
        UsersDbContext db, PasswordHasherService hasher, AdminSeedOptions options, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(options.Email) || string.IsNullOrEmpty(options.Password))
            return false;

        // /auth/register não normaliza o e-mail: grava e compara exatamente como recebido.
        if (await db.Users.AnyAsync(u => u.Email == options.Email, ct))
            return false;

        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = options.Name,
            Email = options.Email,
            Role = UserRole.Admin,
            CreatedAtUtc = DateTime.UtcNow
        };
        user.PasswordHash = hasher.Hash(user, options.Password);

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
