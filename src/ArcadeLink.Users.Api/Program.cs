using System.Text;
using ArcadeLink.Contracts;
using ArcadeLink.Users.Api.Auth;
using ArcadeLink.Users.Api.Caching;
using ArcadeLink.Users.Api.Data;
using ArcadeLink.Users.Api.Domain;
using ArcadeLink.Users.Api.Models;
using ArcadeLink.Users.Api.Validation;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Prometheus;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<PasswordHasherService>();
builder.Services.AddSingleton<JwtTokenService>();

builder.Services.AddStackExchangeRedisCache(options =>
    options.Configuration = builder.Configuration["Redis:ConnectionString"]);

var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing Jwt configuration section.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidAudience = jwtOptions.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMq:Host"], "/", h =>
        {
            h.Username(builder.Configuration["RabbitMq:Username"]!);
            h.Password(builder.Configuration["RabbitMq:Password"]!);
        });
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<UsersDbContext>().Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(feature?.Error, "Unhandled exception while processing {Path}", context.Request.Path);

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            title = "An unexpected error occurred.",
            status = StatusCodes.Status500InternalServerError
        });
    });
});

app.UseHttpsRedirection();
app.UseHttpMetrics();
app.UseAuthentication();
app.UseAuthorization();
app.MapMetrics();

app.MapPost("/auth/register", async (
    RegisterRequest request,
    UsersDbContext db,
    PasswordHasherService passwordHasher,
    IPublishEndpoint publishEndpoint,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(request.Name))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["name"] = ["Name is required."] });

    if (!UserValidation.IsValidEmail(request.Email))
        return Results.ValidationProblem(new Dictionary<string, string[]> { ["email"] = ["Email is invalid."] });

    if (!UserValidation.IsValidPassword(request.Password))
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["password"] = ["Password must be at least 8 characters long and contain letters and digits."]
        });

    var emailExists = await db.Users.AnyAsync(u => u.Email == request.Email);
    if (emailExists)
        return Results.Conflict(new { title = "A user with this email already exists." });

    var user = new User
    {
        Id = Guid.NewGuid(),
        Name = request.Name,
        Email = request.Email,
        Role = UserRole.User,
        CreatedAtUtc = DateTime.UtcNow
    };
    user.PasswordHash = passwordHasher.Hash(user, request.Password);

    db.Users.Add(user);
    await db.SaveChangesAsync();

    await publishEndpoint.Publish(new UserCreatedEvent(user.Id, user.Name, user.Email));
    logger.LogInformation("User {UserId} created and UserCreatedEvent published", user.Id);

    return Results.Created($"/users/{user.Id}", ToResponse(user));
})
.WithName("Register")
.WithOpenApi();

app.MapPost("/auth/login", async (
    LoginRequest request,
    UsersDbContext db,
    PasswordHasherService passwordHasher,
    JwtTokenService tokenService,
    IDistributedCache cache) =>
{
    var cacheKey = $"user-by-email:{request.Email}";
    var user = await JsonDistributedCache.GetAsync<User>(cache, cacheKey);
    if (user is null)
    {
        user = await db.Users.SingleOrDefaultAsync(u => u.Email == request.Email);
        // Cacheia inclusive o hash de senha — aceitável aqui porque o Redis é infraestrutura interna
        // só deste desafio (dev, sem exposição externa); numa API real caberia um DTO sem o hash.
        if (user is not null)
            await JsonDistributedCache.SetAsync(cache, cacheKey, user, TimeSpan.FromSeconds(60));
    }

    if (user is null || !passwordHasher.Verify(user, request.Password))
        return Results.Unauthorized();

    var (token, expiresAtUtc) = tokenService.GenerateToken(user);
    return Results.Ok(new LoginResponse(token, expiresAtUtc));
})
.WithName("Login")
.WithOpenApi();

app.MapGet("/users", async (UsersDbContext db) =>
{
    var users = await db.Users.OrderBy(u => u.CreatedAtUtc).ToListAsync();
    return Results.Ok(users.Select(ToResponse));
})
.RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)))
.WithName("ListUsers")
.WithOpenApi();

app.MapGet("/users/{id:guid}", async (Guid id, UsersDbContext db) =>
{
    var user = await db.Users.FindAsync(id);
    return user is null ? Results.NotFound() : Results.Ok(ToResponse(user));
})
.RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)))
.WithName("GetUser")
.WithOpenApi();

app.MapDelete("/users/{id:guid}", async (Guid id, UsersDbContext db, IDistributedCache cache) =>
{
    var user = await db.Users.FindAsync(id);
    if (user is null)
        return Results.NotFound();

    db.Users.Remove(user);
    await db.SaveChangesAsync();

    await cache.RemoveAsync($"user-by-email:{user.Email}");

    return Results.NoContent();
})
.RequireAuthorization(policy => policy.RequireRole(nameof(UserRole.Admin)))
.WithName("DeleteUser")
.WithOpenApi();

app.Run();

static UserResponse ToResponse(User user) =>
    new(user.Id, user.Name, user.Email, user.Role.ToString(), user.CreatedAtUtc);
