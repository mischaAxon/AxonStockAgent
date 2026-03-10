using AxonStockAgent.Api.Data;
using AxonStockAgent.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AxonStockAgent.Api.Services;

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt,
    UserResponse User
);

public record UserResponse(
    Guid Id,
    string Email,
    string DisplayName,
    string Role
);

public class AuthService
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(AppDbContext db, JwtService jwt, ILogger<AuthService> logger)
    {
        _db = db;
        _jwt = jwt;
        _logger = logger;
    }

    public async Task<AuthResponse> Register(string email, string password, string displayName)
    {
        var normalizedEmail = email.ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail))
            throw new InvalidOperationException("Email already registered");

        var isFirstUser = !await _db.Users.AnyAsync();
        var role = isFirstUser ? "admin" : "user";

        var user = new UserEntity
        {
            Email = normalizedEmail,
            DisplayName = displayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12),
            Role = role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User registered: {Email} with role {Role}", normalizedEmail, role);

        return await GenerateAuthResponse(user);
    }

    public async Task<AuthResponse> Login(string email, string password)
    {
        var normalizedEmail = email.ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalizedEmail)
            ?? throw new UnauthorizedAccessException("Invalid email or password");

        if (!user.IsActive)
            throw new UnauthorizedAccessException("Account is deactivated");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password");

        user.LastLoginAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        _logger.LogInformation("User logged in: {Email}", normalizedEmail);

        return await GenerateAuthResponse(user);
    }

    public async Task<AuthResponse> RefreshToken(string refreshToken)
    {
        var tokenEntity = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == refreshToken && !t.IsRevoked && t.ExpiresAt > DateTime.UtcNow)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token");

        tokenEntity.IsRevoked = true;
        await _db.SaveChangesAsync();

        return await GenerateAuthResponse(tokenEntity.User);
    }

    public async Task Logout(string refreshToken)
    {
        var tokenEntity = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == refreshToken);

        if (tokenEntity != null)
        {
            tokenEntity.IsRevoked = true;
            await _db.SaveChangesAsync();
        }
    }

    private async Task<AuthResponse> GenerateAuthResponse(UserEntity user)
    {
        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = _jwt.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(15);

        _db.RefreshTokens.Add(new RefreshTokenEntity
        {
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays)
        });
        await _db.SaveChangesAsync();

        return new AuthResponse(
            accessToken,
            refreshToken,
            expiresAt,
            new UserResponse(user.Id, user.Email, user.DisplayName, user.Role)
        );
    }
}
