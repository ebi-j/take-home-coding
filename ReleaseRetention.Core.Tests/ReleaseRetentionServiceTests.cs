using FluentAssertions;
using ReleaseRetention.Core.Contracts;
using ReleaseRetention.Core.Services;
using ReleaseRetention.Core.Tests.Helpers;
using Serilog.Sinks.TestCorrelator;
using Serilog;

namespace ReleaseRetention.Core.Tests;

[TestFixture]
public class ReleaseRetentionServiceTests
{
    private Project[] _projects;
    private Release[] _releases;
    private Deployment[] _deployments;
    private DeploymentEnvironment[] _environments;
    private ReleaseRetentionService _service;
    private const string PhantomProjectId = "Project-3";
    private const string PhantomEnvironmentId = "Environment-3";

    [OneTimeSetUp]
    public async Task BeforeAllTests()
    {
        Log.Logger = new LoggerConfiguration().WriteTo.TestCorrelator().CreateLogger();
        _projects = await JsonReader.ReadFromJson<Project[]>("TestData/Projects.json");
        _releases = await JsonReader.ReadFromJson<Release[]>("TestData/Releases.json");
        _deployments = await JsonReader.ReadFromJson<Deployment[]>("TestData/Deployments.json");
        _environments = await JsonReader.ReadFromJson<DeploymentEnvironment[]>("TestData/Environments.json");
        _service = new ReleaseRetentionService(_projects, _releases, _deployments, _environments);
    }

    [Test]
    public void GivenInvalidProjectId_WhenRetain_ShouldThrowArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => _service.RetainReleases(1, PhantomProjectId));
        ex.ParamName.Should().Be("projectId");
    }

    [Test]
    public void GivenInvalidEnvironmentId_WhenRetain_ShouldThrowArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() => _service.RetainReleases(1, environmentId: PhantomEnvironmentId));
        ex.ParamName.Should().Be("environmentId");
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void GivenNonPositiveNumberOfReleasesToKeep_WhenRetain_ShouldThrowArgumentException(int numberOfReleases)
    {
        var ex = Assert.Throws<ArgumentException>(() => _service.RetainReleases(numberOfReleases));
        ex.ParamName.Should().Be("numberOfReleases");
    }

    [Test]
    public void GivenPhantomEnvironmentOrProjectAsInputs_WhenRetain_ShouldExcludeThemFromResolution()
    {
        // Release-8 has phantom Project-3, Deployment-4 has phantom Environment-3
        var resulotions = _service.RetainReleases(_deployments.Length);
        resulotions.Should().NotContain(r => r.EnvironmentId == "Environment-3");
        resulotions.Should().NotContain(r => r.ProjectId == PhantomProjectId);
    }

    [Test]
    public void GivenNoReleaseThatHasNotBeenDeployed_WhenRetain_ShouldExcludeThemFromResolution()
    {
        // Release-3 and Release-4 have not been deployed, thus they should not be in the resolutions
        var resolutions = _service.RetainReleases(_deployments.Length);
        resolutions.Should().NotContain(r => r.ReleasesToKeep.Any(r => r.Id == "Release-3" || r.Id == "Release-4"));
    }

    [Test, TestCaseSource(nameof(TestData))]
    public void AssertReleaseRetention(int numberOfReleasesToKeep, string projectId, string environmentId, string[] releasesToKeep)
    {
        using (TestCorrelator.CreateContext())
        {
            // Action
            var resolutions = _service.RetainReleases(numberOfReleasesToKeep, projectId, environmentId);

            // Assert releases
            AssertReleases(resolutions, numberOfReleasesToKeep, projectId, environmentId, releasesToKeep);

            // Assert logs
            AssertLogs(numberOfReleasesToKeep);
        }
    }

    public static IEnumerable<TestCaseData> TestData
    {
        get
        {
            string[] releasesToAssert1 = ["Release-1"];
            yield return new TestCaseData(1, "Project-1", "Environment-2", releasesToAssert1)
                .SetName("GivenReleaseFromProject1DeployedToEnvironment2_WhenRetain1Release_ShouldReturnRelease1");
            string[] releasesToAssert2 = ["Release-2"];
            yield return new TestCaseData(1, "Project-1", "Environment-1", releasesToAssert2)
                .SetName("GivenReleasesFromProject1DeployedToEnvironment1_WhenRetain1Release_ShouldReturnRelease2");
            string[] releasesToAssert3 = ["Release-1", "Release-2"];
            yield return new TestCaseData(2, "Project-1", "Environment-1", releasesToAssert3)
                .SetName("GivenReleasesFromProject1DeployedToEnvironment1_WhenRetain2Releases_ShouldReturnRelease1And2");
            string[] releasesToAssert4 = ["Release-5", "Release-6", "Release-7"];
            yield return new TestCaseData(3, "Project-2", "Environment-1", releasesToAssert4)
                .SetName("GivenReleasesFromProject2DeployedToEnvironment1_WhenRetain3Releases_ShouldReturnRelease5And6And7");
        }
    }

    [Test]
    public void GivenReleasesFromProject1DeployedToEnvironment1And2_WhenRetain1ReleaseForEachEnvironment_ShouldKeepRelease2ForEnvironment1AndRelease1ForEnvironment2()
    {
        const int numberOfReleasesToKeep = 1;
        const string projectId = "Project-1";

        using (TestCorrelator.CreateContext())
        {
            // Action
            var resolutions = _service.RetainReleases(numberOfReleasesToKeep, projectId);

            // Assert releases for environment 1
            AssertReleases(resolutions, numberOfReleasesToKeep, projectId, "Environment-1", ["Release-2"]);
            // Assert releases for environment 2
            AssertReleases(resolutions, numberOfReleasesToKeep, projectId, "Environment-2", ["Release-1"]);

            // Assert logs - we have checked two environments
            AssertLogs(numberOfReleasesToKeep * 2);
        }
    }

    [Test]
    public void GivenReleasesDeployedToEnvironment1_WhenRetain1ReleaseForEachProject_ShouldKeepRelease2ForProject1AndRelease6ForProject2()
    {
        const int numberOfReleasesToKeep = 1;
        const string environmentId = "Environment-1";

        using (TestCorrelator.CreateContext())
        {
            // Action
            var resolutions = _service.RetainReleases(numberOfReleasesToKeep, environmentId: environmentId);

            // Assert releases for project 1
            AssertReleases(resolutions, numberOfReleasesToKeep, "Project-1", environmentId, ["Release-2"]);
            // Assert releases for project 2
            AssertReleases(resolutions, numberOfReleasesToKeep, "Project-2", environmentId, ["Release-6"]);

            // Assert logs - we have checked two environments
            AssertLogs(numberOfReleasesToKeep * 2);
        }
    }

    private static void AssertReleases(List<ReleaseRetentionResolution> resolutions, int numberOfReleasesToKeep, string projectId, string environmentId, string[] releasesToKeep)
    {
        resolutions.Should().Contain(r =>
                        r.ProjectId.Equals(projectId)
                        && r.EnvironmentId.Equals(environmentId)
                        && r.ReleasesToKeep.Count == numberOfReleasesToKeep
                        && r.ReleasesToKeep.Any(release => releasesToKeep.Contains(release.Id)));
    }

    private static void AssertLogs(int numberOfReleasesToKeep)
    {
        var logEvents = TestCorrelator.GetLogEventsFromCurrentContext();
        logEvents.Should().HaveCount(numberOfReleasesToKeep);
        logEvents.ToList().ForEach(e => e.MessageTemplate.Text.Should().Be(ReleaseRetentionService.ReasonLog));
    }
}