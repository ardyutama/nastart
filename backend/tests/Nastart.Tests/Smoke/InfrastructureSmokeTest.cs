using AwesomeAssertions;
using NSubstitute;
using Nastart.Application.Common.Interfaces;

namespace Nastart.Tests.Smoke;

public class InfrastructureSmokeTest
{
    [Fact]
    public void TestInfrastructure_ShouldWork()
    {
        var db = Substitute.For<IAppDbContext>();

        db.Should().NotBeNull("NSubstitute and AwesomeAssertions packages are installed correctly");
    }
}