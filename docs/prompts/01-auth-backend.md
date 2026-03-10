# Prompt 01 — Auth Backend (JWT + Admin/User rollen)

Deze prompt is bedoeld voor Claude Code.

---

## Prompt

```
Werk in de repository: mischaAxon/AxonStockAgent

Maak een nieuwe branch `feature/auth` aan vanaf `main`.

Implementeer JWT authenticatie met Admin en User rollen in het .NET 8 backend project.

### 1. NuGet packages toevoegen aan AxonStockAgent.Api.csproj:
- Microsoft.AspNetCore.Authentication.JwtBearer 8.0.4
- BCrypt.Net-Next 4.0.3
- System.IdentityModel.Tokens.Jwt 7.5.1

### 2. Maak Data/Entities/UserEntity.cs:
```csharp
namespace AxonStockAgent.Api.Data.Entities;

public class UserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Role { get; set; } = "user"; // "admin" of "user"
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
```

### 3. Maak Data/Entities/RefreshTokenEntity.cs:
```csharp
namespace AxonStockAgent.Api.Data.Entities;

public class RefreshTokenEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; } = false;
    public UserEntity User { get; set; } = null!;
}
```

### 4. Update AppDbContext.cs:
Voeg DbSet<UserEntity> Users en DbSet<RefreshTokenEntity> RefreshTokens toe.
Voeg OnModelCreating configuratie toe:
- Users tabel "users", unieke index op Email
- RefreshTokens tabel "refresh_tokens", index op Token en UserId
- FK van RefreshToken naar User

### 5. Maak Services/JwtService.cs:
- Constructor neemt IConfiguration
- Lees JWT settings uit config: Secret (min 32 chars), Issuer, Audience, AccessTokenMinutes (15), RefreshTokenDays (30)
- Method: GenerateAccessToken(UserEntity user) -> string (JWT met claims: sub=userId, email, role, exp)
- Method: GenerateRefreshToken() -> string (random 64 bytes -> base64)
- Method: ValidateToken(string token) -> ClaimsPrincipal? (validate zonder lifetime check voor refresh flow)

### 6. Maak Services/AuthService.cs:
- Constructor: AppDbContext, JwtService, ILogger
- Method: Register(string email, string password, string displayName) -> AuthResponse
  - Check of email al bestaat -> throw als ja
  - Als er nog GEEN users in de database zijn -> role = "admin" (eerste user is altijd admin)
  - Anders role = "user"
  - Hash password met BCrypt (workfactor 12)
  - Sla user op
  - Genereer access + refresh token
  - Return AuthResponse
- Method: Login(string email, string password) -> AuthResponse
  - Zoek user op email
  - Verify password met BCrypt
  - Update LastLoginAt
  - Genereer access + refresh token
  - Sla refresh token op in database
  - Return AuthResponse
- Method: RefreshToken(string refreshToken) -> AuthResponse
  - Zoek token in database (niet expired, niet revoked)
  - Revoke oude token
  - Genereer nieuwe access + refresh token
  - Return AuthResponse
- Method: Logout(string refreshToken) -> void
  - Revoke de refresh token

AuthResponse record:
```csharp
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
```

### 7. Maak Controllers/AuthController.cs:
Route: api/v1/auth

- POST /register -> body: { email, password, displayName } -> AuthResponse
- POST /login -> body: { email, password } -> AuthResponse  
- POST /refresh -> body: { refreshToken } -> AuthResponse
- POST /logout -> body: { refreshToken } -> 204
- GET /me -> [Authorize] -> UserResponse (huidige user uit JWT claims)

Alles met try/catch, nette error responses.

### 8. Maak Controllers/AdminController.cs:
Route: api/v1/admin
Alle endpoints: [Authorize(Roles = "admin")]

- GET /users -> lijst van alle users (zonder password hashes)
- PUT /users/{id} -> update user role of isActive
- GET /settings -> placeholder, return lege JSON object {}
- PUT /settings -> placeholder, return OK

### 9. Update Program.cs:
- Voeg JWT authentication toe met builder.Services.AddAuthentication().AddJwtBearer()
- Voeg Authorization toe met builder.Services.AddAuthorization()
- Registreer JwtService en AuthService als scoped services
- Voeg app.UseAuthentication() en app.UseAuthorization() toe (voor MapControllers)
- Voeg JWT configuratie toe aan appsettings.json:
```json
"Jwt": {
  "Secret": "AxonStockAgent-Dev-Secret-Key-Change-In-Production-Min32Chars!",
  "Issuer": "AxonStockAgent",
  "Audience": "AxonStockAgent",
  "AccessTokenMinutes": 15,
  "RefreshTokenDays": 30
}
```

### 10. Update database/init.sql:
Voeg de users en refresh_tokens tabellen toe:
```sql
CREATE TABLE IF NOT EXISTS users (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    email VARCHAR(200) NOT NULL UNIQUE,
    password_hash TEXT NOT NULL,
    display_name VARCHAR(100),
    role VARCHAR(20) NOT NULL DEFAULT 'user',
    is_active BOOLEAN DEFAULT true,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    last_login_at TIMESTAMPTZ
);

CREATE TABLE IF NOT EXISTS refresh_tokens (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    token TEXT NOT NULL,
    expires_at TIMESTAMPTZ NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    is_revoked BOOLEAN DEFAULT false
);

CREATE INDEX idx_refresh_tokens_token ON refresh_tokens(token);
CREATE INDEX idx_refresh_tokens_user ON refresh_tokens(user_id);
```

### 11. Bescherm bestaande controllers:
- WatchlistController, SignalsController, PortfolioController, DashboardController: voeg [Authorize] toe op class level
- AdminController: voeg [Authorize(Roles = "admin")] toe
- AuthController: geen [Authorize] op register/login/refresh, wel op /me

### 12. Update .env.example:
Voeg toe:
```
JWT_SECRET=change-this-to-a-long-random-string-minimum-32-characters
```

Commit alles met message: "feat(auth): JWT authentication with admin/user roles"
Push naar branch feature/auth.
```
