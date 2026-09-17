using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using GenericTestWebApi.Entities;

namespace GenericTestWebApi.Auth;

public class JwtTokenService(IConfiguration configuration)
{
    private readonly JwtOptions options = configuration.GetSection("Jwt").Get<JwtOptions>() ?? new JwtOptions();

    public string CreateAccessToken(UserEntity user, IEnumerable<string> roles)
    {
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new(ClaimTypes.NameIdentifier, user.Id.ToString()), new(ClaimTypes.Name, user.FullName), new(ClaimTypes.Email, user.Email) };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(options.Issuer, options.Audience, claims, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(options.AccessTokenMinutes), credentials));
    }

    public (string RawToken, string Hash, DateTime ExpiresAtUtc) CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
        return (raw, hash, DateTime.UtcNow.AddDays(options.RefreshTokenDays));
    }

    public string HashRefreshToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}