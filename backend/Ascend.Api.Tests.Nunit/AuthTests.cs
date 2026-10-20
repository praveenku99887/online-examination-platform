using Ascend.Api.Tests.Shared;
using NUnit.Framework;

namespace Ascend.Api.Tests.Nunit;

[TestFixture]
public sealed class AuthTests
{
    [Test]
    public async Task Signup_rejects_short_name()
    {
        using var api = new ApiClient(GlobalSetup.Factory);

        var response = await api.SignupAsync(
            "A", TestData.UniqueEmail("short-name"), role: "teacher");

        Assert.That(response.StatusCode, Is.EqualTo(400));
        Assert.That(response.Error, Is.EqualTo("Please enter your full name."));
    }

    [Test]
    public async Task Signup_rejects_invalid_email()
    {
        using var api = new ApiClient(GlobalSetup.Factory);

        var response = await api.SignupAsync(
            "Test Teacher", "not-an-email", role: "teacher");

        Assert.That(response.StatusCode, Is.EqualTo(400));
        Assert.That(response.Error, Is.EqualTo("Enter a valid email address."));
    }

    [Test]
    public async Task Signup_rejects_short_password()
    {
        using var api = new ApiClient(GlobalSetup.Factory);

        var response = await api.SignupAsync(
            "Test Teacher", TestData.UniqueEmail(), password: "123", role: "teacher");

        Assert.That(response.StatusCode, Is.EqualTo(400));
        Assert.That(response.Error, Is.EqualTo("Password must be at least 6 characters."));
    }

    [Test]
    public async Task Signup_returns_cookie_and_authenticates_me()
    {
        var email = TestData.UniqueEmail("signup");
        using var api = new ApiClient(GlobalSetup.Factory);

        var response = await api.SignupAsync("Test Student", email, role: "student");

        Assert.That(response.StatusCode, Is.EqualTo(200));
        Assert.That(response.Body.GetProperty("user").GetProperty("email").GetString(),
            Is.EqualTo(email));
        Assert.That(
            response.SetCookie.Any(c => c.StartsWith("ax_session=", StringComparison.OrdinalIgnoreCase)),
            Is.True);
        Assert.That(
            response.SetCookie.Single(c => c.StartsWith("ax_session=", StringComparison.OrdinalIgnoreCase))
                .ToLowerInvariant(),
            Does.Contain("httponly"));

        var me = await api.MeAsync();
        Assert.That(me.StatusCode, Is.EqualTo(200));
        Assert.That(me.Body.GetProperty("user").GetProperty("email").GetString(),
            Is.EqualTo(email));
    }

    [Test]
    public async Task Jwt_authenticates_without_cookie()
    {
        var email = TestData.UniqueEmail("jwt");
        using var signupClient = new ApiClient(GlobalSetup.Factory);
        var signup = await signupClient.SignupAsync("Test Student", email, role: "student");
        var token = signup.Body.GetProperty("token").GetString();

        Assert.That(token, Is.Not.Null.And.Not.Empty);

        using var bearerClient = new ApiClient(GlobalSetup.Factory);
        bearerClient.UseBearerToken(token);

        var me = await bearerClient.MeAsync();
        Assert.That(me.StatusCode, Is.EqualTo(200));
        Assert.That(me.Body.GetProperty("user").GetProperty("email").GetString(),
            Is.EqualTo(email));
    }

    [Test]
    public async Task Duplicate_email_is_rejected()
    {
        var email = TestData.UniqueEmail("duplicate");
        using var api = new ApiClient(GlobalSetup.Factory);

        var first = await api.SignupAsync("First Teacher", email, role: "teacher");
        var second = await api.SignupAsync("Second Teacher", email, role: "teacher");

        Assert.That(first.StatusCode, Is.EqualTo(200));
        Assert.That(second.StatusCode, Is.EqualTo(409));
        Assert.That(second.Error, Is.EqualTo("An account with this email already exists."));
    }

    [Test]
    public async Task Login_and_logout_flow()
    {
        var email = TestData.UniqueEmail("login");
        using var api = new ApiClient(GlobalSetup.Factory);
        await api.SignupAsync("Test Student", email, role: "student");
        await api.LogoutAsync();

        Assert.That((await api.MeAsync()).StatusCode, Is.EqualTo(401));

        var login = await api.LoginAsync(email, "secret123");
        Assert.That(login.StatusCode, Is.EqualTo(200));
        Assert.That((await api.MeAsync()).StatusCode, Is.EqualTo(200));

        var wrong = await api.LoginAsync(email, "wrong-password");
        Assert.That(wrong.StatusCode, Is.EqualTo(401));
        Assert.That(wrong.Error, Is.EqualTo("Invalid email or password."));
    }

    [Test]
    public async Task Demo_student_is_available()
    {
        using var api = new ApiClient(GlobalSetup.Factory);

        var response = await api.DemoAsync("student");

        Assert.That(response.StatusCode, Is.EqualTo(200));
        Assert.That(response.Body.GetProperty("role").GetString(), Is.EqualTo("student"));
        Assert.That((await api.MeAsync()).StatusCode, Is.EqualTo(200));
    }
}
