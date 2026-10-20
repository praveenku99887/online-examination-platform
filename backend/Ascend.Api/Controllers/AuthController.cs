using System.Text.RegularExpressions;
using Ascend.Api.Data;
using Ascend.Api.Events;
using Ascend.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Ascend.Api.Controllers;

[Route("api/auth")]
public class AuthController : ApiControllerBase
{
    private readonly IAscendEventPublisher _events;

    public AuthController(IOptions<JwtOptions> jwt, IAscendEventPublisher events)
        : base(jwt) => _events = events;

    private object AuthPayload(UserRow user)
    {
        var sessionUser = new SessionUser(user.Id, user.Name, user.Email, user.Role);
        var token = string.IsNullOrWhiteSpace(Jwt.Key) ? null : JwtService.CreateToken(Jwt, sessionUser);
        return new
        {
            ok = true,
            role = user.Role,
            name = user.Name,
            token,
            user = new { id = user.Id, name = user.Name, email = user.Email, role = user.Role },
        };
    }

    [HttpPost("signup")]
    public async Task<IActionResult> Signup()
    {
        try
        {
            var body = await ReadBodyAsync();
            if (body == null) return Err("Something went wrong. Try again.", 500);
            var name = GetStr(body, "name")?.Trim();
            var email = GetStr(body, "email")?.Trim().ToLowerInvariant();
            var password = GetStr(body, "password") ?? "";
            var role = GetStr(body, "role");

            if (string.IsNullOrEmpty(name) || name.Length < 2)
                return Err("Please enter your full name.", 400);
            if (string.IsNullOrEmpty(email) || !Regex.IsMatch(email, "^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$"))
                return Err("Enter a valid email address.", 400);
            if (password.Length < 6)
                return Err("Password must be at least 6 characters.", 400);
            if (role != "teacher" && role != "student")
                return Err("Choose a role: teacher or student.", 400);

            await using var conn = await Db.OpenConnectionAsync();
            var existingId = await conn.QueryFirstOrDefaultAsync<string>(
                "SELECT id FROM users WHERE email = @email", new { email });
            if (existingId != null)
                return Err("An account with this email already exists.", 409);

            var id = Db.NewId();
            await conn.ExecuteAsync(
                "INSERT INTO users (id, name, email, password_hash, role, created_at) VALUES (@id, @name, @email, @passwordHash, @role, @now)",
                new { id = id, name, email, passwordHash = Passwords.Hash(password), role, now = Db.NowDb() });
            var user = await conn.QuerySingleAsync<UserRow>(
                "SELECT * FROM users WHERE id = @id", new { id });

            await SessionService.CreateAsync(Response, user.Id);
            _events.Publish(AscendEventTypes.UserSignedUp, user.Id.ToString(), new { user.Id, user.Name, user.Email, user.Role });
            return Json(AuthPayload(user));
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[auth:signup] {ex}");
            return Err("Something went wrong. Try again.", 500);
        }
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login()
    {
        try
        {
            var body = await ReadBodyAsync();
            if (body == null) return Err("Something went wrong. Try again.", 500);
            var email = GetStr(body, "email")?.Trim().ToLowerInvariant();
            var password = GetStr(body, "password") ?? "";
            if (string.IsNullOrEmpty(email) || password.Length == 0)
                return Err("Email and password are required.", 400);

            await using var conn = await Db.OpenConnectionAsync();
            var user = await conn.QueryFirstOrDefaultAsync<UserRow>(
                "SELECT * FROM users WHERE email = @email", new { email });
            if (user == null || !Passwords.Verify(password, user.PasswordHash))
                return Err("Invalid email or password.", 401);

            await SessionService.CreateAsync(Response, user.Id);
            _events.Publish(AscendEventTypes.UserLoggedIn, user.Id.ToString(), new { user.Id, user.Email, user.Role });
            return Json(AuthPayload(user));
        }
        catch
        {
            return Err("Something went wrong. Try again.", 500);
        }
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await SessionService.DestroyAsync(HttpContext);
            return Json(new { ok = true });
        }
        catch
        {
            return Err("Something went wrong. Try again.", 500);
        }
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var user = await CurrentUser();
        if (user == null) return Err("Not authenticated", 401);
        return Json(new { user = new { id = user.Id, name = user.Name, email = user.Email, role = user.Role } });
    }

    [HttpPost("demo")]
    public async Task<IActionResult> Demo()
    {
        try
        {
            var body = await ReadBodyAsync();
            var role = GetStr(body, "role") == "teacher" ? "teacher" : "student";
            var user = await DemoSeed.GetOrCreateDemoUserAsync(role);
            await SessionService.CreateAsync(Response, user.Id);
            _events.Publish(AscendEventTypes.UserLoggedIn, user.Id.ToString(), new { user.Id, user.Email, user.Role, demo = true });
            return Json(new
            {
                ok = true,
                role = user.Role,
                name = user.Name,
                token = string.IsNullOrWhiteSpace(Jwt.Key) ? null : JwtService.CreateToken(Jwt, user),
                user = new { id = user.Id, name = user.Name, email = user.Email, role = user.Role },
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[demo] {ex}");
            return Err("Could not start demo session.", 500);
        }
    }
}
