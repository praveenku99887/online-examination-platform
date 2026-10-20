using Ascend.Api.Tests.Shared;
using Xunit;

namespace Ascend.Api.Tests.Xunit;

[Collection(ApiCollection.Name)]
public sealed class AuthTests
{
    private readonly AscendWebApplicationFactory _factory;

    public AuthTests(AscendWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Signup_rejects_short_name()
    {
        using var api = new ApiClient(_factory);

        var response = await api.SignupAsync(
            "A", TestData.UniqueEmail("short-name"), role: "teacher");

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Please enter your full name.", response.Error);
    }

    [Fact]
    public async Task Signup_rejects_invalid_email()
    {
        using var api = new ApiClient(_factory);

        var response = await api.SignupAsync("Test Teacher", "not-an-email", role: "teacher");

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Enter a valid email address.", response.Error);
    }

    [Fact]
    public async Task Signup_rejects_short_password()
    {
        using var api = new ApiClient(_factory);

        var response = await api.SignupAsync(
            "Test Teacher", TestData.UniqueEmail(), password: "123", role: "teacher");

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Password must be at least 6 characters.", response.Error);
    }

    [Fact]
    public async Task Signup_rejects_unknown_role()
    {
        using var api = new ApiClient(_factory);

        var response = await api.SignupAsync(
            "Test Teacher", TestData.UniqueEmail(), role: "admin");

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("Choose a role: teacher or student.", response.Error);
    }

    [Fact]
    public async Task Signup_returns_user_cookie_and_authenticated_me()
    {
        var email = TestData.UniqueEmail("signup");
        using var api = new ApiClient(_factory);

        var response = await api.SignupAsync("Test Teacher", email, role: "teacher");

        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Body.GetProperty("ok").GetBoolean());
        Assert.Equal(email, response.Body.GetProperty("user").GetProperty("email").GetString());

        var cookie = Assert.Single(
            response.SetCookie,
            c => c.StartsWith("ax_session=", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("httponly", cookie.ToLowerInvariant());
        Assert.Contains("samesite=lax", cookie.ToLowerInvariant());

        var me = await api.MeAsync();

        Assert.Equal(200, me.StatusCode);
        Assert.Equal(email, me.Body.GetProperty("user").GetProperty("email").GetString());
        Assert.Equal("teacher", me.Body.GetProperty("user").GetProperty("role").GetString());
    }

    [Fact]
    public async Task Signup_returns_jwt_that_authenticates_without_cookie()
    {
        var email = TestData.UniqueEmail("jwt");
        using var signupClient = new ApiClient(_factory);

        var signup = await signupClient.SignupAsync("Test Student", email, role: "student");
        var token = signup.Body.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(token));

        using var bearerClient = new ApiClient(_factory);
        bearerClient.UseBearerToken(token);

        var me = await bearerClient.MeAsync();

        Assert.Equal(200, me.StatusCode);
        Assert.Equal(email, me.Body.GetProperty("user").GetProperty("email").GetString());
    }

    [Fact]
    public async Task Signup_rejects_duplicate_email()
    {
        var email = TestData.UniqueEmail("duplicate");
        using var api = new ApiClient(_factory);

        var first = await api.SignupAsync("First Teacher", email, role: "teacher");
        var second = await api.SignupAsync("Second Teacher", email, role: "teacher");

        Assert.Equal(200, first.StatusCode);
        Assert.Equal(409, second.StatusCode);
        Assert.Equal("An account with this email already exists.", second.Error);
    }

    [Fact]
    public async Task Login_rejects_wrong_password()
    {
        var email = TestData.UniqueEmail("wrong-pw");
        using var api = new ApiClient(_factory);
        await api.SignupAsync("Test Teacher", email, role: "teacher");

        var response = await api.LoginAsync(email, "not-the-password");

        Assert.Equal(401, response.StatusCode);
        Assert.Equal("Invalid email or password.", response.Error);
    }

    [Fact]
    public async Task Login_succeeds_and_logout_clears_session()
    {
        var email = TestData.UniqueEmail("login");
        using var api = new ApiClient(_factory);
        await api.SignupAsync("Test Student", email, role: "student");
        await api.LogoutAsync();

        var meAfterLogout = await api.MeAsync();
        Assert.Equal(401, meAfterLogout.StatusCode);

        var login = await api.LoginAsync(email, "secret123");
        Assert.Equal(200, login.StatusCode);
        Assert.Equal("student", login.Body.GetProperty("role").GetString());

        var me = await api.MeAsync();
        Assert.Equal(200, me.StatusCode);

        var logout = await api.LogoutAsync();
        Assert.Equal(200, logout.StatusCode);
        Assert.Equal(401, (await api.MeAsync()).StatusCode);
    }

    [Fact]
    public async Task Demo_returns_a_seeded_student_session()
    {
        using var api = new ApiClient(_factory);

        var response = await api.DemoAsync("student");

        Assert.Equal(200, response.StatusCode);
        Assert.True(response.Body.GetProperty("ok").GetBoolean());
        Assert.Equal("student", response.Body.GetProperty("role").GetString());
        Assert.Contains(
            response.SetCookie,
            cookie => cookie.StartsWith("ax_session=", StringComparison.OrdinalIgnoreCase));

        var me = await api.MeAsync();
        Assert.Equal(200, me.StatusCode);
        Assert.Equal("student", me.Body.GetProperty("user").GetProperty("role").GetString());
    }
}
