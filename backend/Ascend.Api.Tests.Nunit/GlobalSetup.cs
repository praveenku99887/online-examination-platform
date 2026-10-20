using Ascend.Api.Tests.Shared;
using NUnit.Framework;

namespace Ascend.Api.Tests.Nunit;

[SetUpFixture]
public sealed class GlobalSetup
{
    public static AscendWebApplicationFactory Factory { get; private set; } = null!;

    [OneTimeSetUp]
    public async Task StartAsync()
    {
        Factory = new AscendWebApplicationFactory();
        using var api = new ApiClient(Factory);
        var response = await api.GetAsync("/api/health");
        Assert.That(response.StatusCode, Is.EqualTo(200));
    }

    [OneTimeTearDown]
    public void Stop() => Factory.Dispose();
}
