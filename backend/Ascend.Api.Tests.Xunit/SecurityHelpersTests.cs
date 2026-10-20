using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ascend.Api.Data;
using Ascend.Api.Services;
using Ascend.Api.Tests.Shared;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace Ascend.Api.Tests.Xunit;

public sealed class SecurityHelpersTests
{
    [Fact]
    public void Password_hash_round_trips()
    {
        var hash = Passwords.Hash("secret123");

        Assert.NotEqual("secret123", hash);
        Assert.True(Passwords.Verify("secret123", hash));
        Assert.False(Passwords.Verify("wrong-password", hash));
    }

    [Fact]
    public void Password_hashes_are_salted()
    {
        var first = Passwords.Hash("same-password");
        var second = Passwords.Hash("same-password");

        Assert.NotEqual(first, second);
        Assert.True(Passwords.Verify("same-password", first));
        Assert.True(Passwords.Verify("same-password", second));
    }

    [Fact]
    public void Password_verify_returns_false_for_invalid_hash()
    {
        Assert.False(Passwords.Verify("secret123", "not-a-bcrypt-hash"));
    }

    [Fact]
    public void Jwt_round_trips_user_identity()
    {
        var options = new JwtOptions
        {
            Key = AscendWebApplicationFactory.TestJwtKey,
            Issuer = "Ascend",
            Audience = "Ascend",
            ExpiryMinutes = 60,
        };
        var user = new SessionUser(
            Guid.NewGuid(), "Test Teacher", "teacher@example.com", "teacher");

        var token = JwtService.CreateToken(options, user);
        var principal = new JwtSecurityTokenHandler().ValidateToken(
            token,
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = options.Issuer,
                ValidAudience = options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(options.Key)),
                ClockSkew = TimeSpan.Zero,
            },
            out _);

        var parsed = JwtService.ReadUser(principal);

        Assert.NotNull(parsed);
        Assert.Equal(user.Id, parsed!.Id);
        Assert.Equal(user.Name, parsed.Name);
        Assert.Equal(user.Email, parsed.Email);
        Assert.Equal(user.Role, parsed.Role);
    }

    [Fact]
    public void Jwt_read_user_rejects_unknown_role()
    {
        var principal = Principal("teacher", "admin");

        Assert.Null(JwtService.ReadUser(principal));
    }

    [Fact]
    public void Jwt_read_user_rejects_invalid_guid()
    {
        var principal = Principal("not-a-guid", "teacher");

        Assert.Null(JwtService.ReadUser(principal));
    }

    private static ClaimsPrincipal Principal(string id, string role) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, id),
            new Claim(ClaimTypes.Name, "Test User"),
            new Claim(ClaimTypes.Email, "test@example.com"),
            new Claim(ClaimTypes.Role, role),
        }, "test"));
}
