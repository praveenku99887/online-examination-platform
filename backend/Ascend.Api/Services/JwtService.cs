using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ascend.Api.Data;
using Microsoft.IdentityModel.Tokens;

namespace Ascend.Api.Services;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "Ascend";
    public string Audience { get; set; } = "Ascend";
    public int ExpiryMinutes { get; set; } = 60 * 24 * 7;
}

public static class JwtService
{
    public static string CreateToken(JwtOptions options, SessionUser user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
        };
        var token = new JwtSecurityToken(
            issuer: options.Issuer,
            audience: options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(options.ExpiryMinutes),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static SessionUser? ReadUser(ClaimsPrincipal principal)
    {
        var idRaw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(idRaw, out var id)) return null;
        var name = principal.FindFirstValue(ClaimTypes.Name) ?? "";
        var email = principal.FindFirstValue(ClaimTypes.Email) ?? "";
        var role = principal.FindFirstValue(ClaimTypes.Role) ?? "";
        if (role != "teacher" && role != "student") return null;
        return new SessionUser(id, name, email, role);
    }
}
