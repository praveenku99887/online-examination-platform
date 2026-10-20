using System.Security.Cryptography;
using Ascend.Api.Data;
using Dapper;

namespace Ascend.Api.Services;

public static class SessionService
{
    public const string CookieName = "ax_session";
    public const int SessionDays = 7;

    public static string GenerateToken() =>
        Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();

    public static void SetCookie(HttpResponse res, string token, DateTime expiresAtUtc) =>
        res.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            Expires = expiresAtUtc,
        });

    public static void ClearCookie(HttpResponse res) =>
        res.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });

    public static async Task<string> CreateAsync(HttpResponse res, Guid userId)
    {
        var token = GenerateToken();
        var expiresAt = Clock.UtcNowMs().AddDays(SessionDays);
        await using var conn = await Db.OpenConnectionAsync();
        await conn.ExecuteAsync(
            "INSERT INTO sessions (id, user_id, token, expires_at, created_at) VALUES (@id, @userId, @token, @expiresAt, @now)",
            new { id = Db.NewId(), userId = userId.ToString(), token, expiresAt = Db.ToDb(expiresAt), now = Db.NowDb() });
        SetCookie(res, token, expiresAt);
        return token;
    }

    public static async Task<SessionUser?> GetUserAsync(HttpContext ctx)
    {
        var token = ctx.Request.Cookies[CookieName];
        if (string.IsNullOrEmpty(token)) return null;
        await using var conn = await Db.OpenConnectionAsync();
        var row = await conn.QueryFirstOrDefaultAsync<UserRow>(
            "SELECT u.id, u.name, u.email, u.role FROM sessions s JOIN users u ON s.user_id = u.id WHERE s.token = @token AND s.expires_at > @now",
            new { token, now = Db.NowDb() });
        return row == null ? null : new SessionUser(row.Id, row.Name, row.Email, row.Role);
    }

    public static async Task DestroyAsync(HttpContext ctx)
    {
        var token = ctx.Request.Cookies[CookieName];
        if (!string.IsNullOrEmpty(token))
        {
            await using var conn = await Db.OpenConnectionAsync();
            await conn.ExecuteAsync("DELETE FROM sessions WHERE token = @token", new { token });
        }
        ClearCookie(ctx.Response);
    }
}
