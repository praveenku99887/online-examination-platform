using Ascend.Api.Tests.Shared;
using Xunit;

namespace Ascend.Api.Tests.Xunit;

[CollectionDefinition("ascend-api", DisableParallelization = true)]
public sealed class ApiCollection : ICollectionFixture<AscendWebApplicationFactory>
{
    public const string Name = "ascend-api";
}
