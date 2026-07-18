using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NodeReel.Application.Abstractions;
using NodeReel.Domain.Entities;
using NodeReel.Domain.Enums;
using NodeReel.Infrastructure.Options;
using NodeReel.Infrastructure.Persistence;

namespace NodeReel.Infrastructure.Auth;

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly AuthOptions _options;

    public AuthService(AppDbContext db, IOptions<AuthOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<LoginResultDto?> LoginAsync(LoginRequestDto request, CancellationToken ct = default)
    {
        var username = request.Username.Trim();
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(request.Password))
            return null;

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == username, ct);
        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash))
            return null;

        return new LoginResultDto
        {
            Token = CreateToken(user),
            User = Map(user)
        };
    }

    public async Task<UserDto?> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        return user is null ? null : Map(user);
    }

    private string CreateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _options.JwtIssuer,
            audience: _options.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_options.TokenLifetimeHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    internal static UserDto Map(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Role = user.Role,
        CreatedAt = user.CreatedAt
    };
}

public sealed class UserAdminService : IUserAdminService
{
    private readonly AppDbContext _db;

    public UserAdminService(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<UserDto>> ListAsync(CancellationToken ct = default)
    {
        var users = await _db.Users.OrderBy(u => u.Username).ToListAsync(ct);
        return users.Select(AuthService.Map).ToList();
    }

    public async Task<UserDto> CreateAsync(CreateUserDto dto, CancellationToken ct = default)
    {
        var username = dto.Username.Trim();
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required.");
        if (string.IsNullOrWhiteSpace(dto.Password) || dto.Password.Length < 4)
            throw new ArgumentException("Password must be at least 4 characters.");

        if (await _db.Users.AnyAsync(u => u.Username == username, ct))
            throw new InvalidOperationException($"Username '{username}' is already taken.");

        var user = new User
        {
            Username = username,
            PasswordHash = PasswordHasher.Hash(dto.Password),
            Role = dto.Role
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);
        return AuthService.Map(user);
    }

    public async Task ChangePasswordAsync(Guid id, ChangePasswordDto dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.NewPassword) || dto.NewPassword.Length < 4)
            throw new ArgumentException("Password must be at least 4 characters.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new KeyNotFoundException($"User '{id}' not found.");

        user.PasswordHash = PasswordHasher.Hash(dto.NewPassword);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, Guid actingAdminId, CancellationToken ct = default)
    {
        if (id == actingAdminId)
            throw new InvalidOperationException("You cannot delete your own account.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return;

        if (user.Role == UserRole.Admin)
        {
            var adminCount = await _db.Users.CountAsync(u => u.Role == UserRole.Admin, ct);
            if (adminCount <= 1)
                throw new InvalidOperationException("Cannot delete the last admin.");
        }

        _db.Users.Remove(user);
        await _db.SaveChangesAsync(ct);
    }
}
