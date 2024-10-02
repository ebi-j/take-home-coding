using ReleaseRetention.Core.Contracts;
using Serilog;

namespace ReleaseRetention.Core.Services;

/// <summary>
/// A service for release retention query
/// </summary>
public class ReleaseRetentionService
{
    public const string ReasonLog = "{ReleaseId} (version: {ReleaseVersion}) kept for {ProjectName} ({projectId}) because it was top {Index} deployed to {EnvironmentName} ({EnvironmentId})";

    private readonly Project[] _projects;
    private readonly Release[] _releases;
    private readonly Deployment[] _deployments;
    private readonly DeploymentEnvironment[] _environments;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="projects">Projects for the releases</param>
    /// <param name="releases">Releases to be queried</param>
    /// <param name="deployments">Deployments of the releases</param>
    /// <param name="deploymentEnvironments">Environments that deployments have deployed to</param>
    public ReleaseRetentionService(
        Project[] projects,
        Release[] releases,
        Deployment[] deployments,
        DeploymentEnvironment[] deploymentEnvironments
    )
    {
        _projects = projects;
        _releases = releases;
        _deployments = deployments;
        _environments = deploymentEnvironments;
    }

    /// <summary>
    /// Returns a list of resolution of a certain number of releases to be kept against per project and environment combination from the inputs
    /// </summary>
    /// <param name="numberOfReleases">Number of releases to keep</param>
    /// <param name="projectId">Optional. Return the resolution to the project when provided</param>
    /// <param name="environmentId">Optional. Return the resolution to the environment when provided</param>
    /// <returns>Release retention resolution list</returns>
    public List<ReleaseRetentionResolution> RetainReleases(int numberOfReleases, string? projectId = default, string? environmentId = default)
    {
        if (numberOfReleases <= 0)
        {
            throw new ArgumentException("Number of releases should be greater than zero.", nameof(numberOfReleases));
        }

        if (!string.IsNullOrEmpty(projectId) && !_projects.Any(p => p.Id == projectId))
        {
            throw new ArgumentException("Project id does not seem to be in the project list provided.", nameof(projectId));
        }

        if (!string.IsNullOrEmpty(environmentId) && !_environments.Any(e => e.Id == environmentId))
        {
            throw new ArgumentException("Environment id does not seem to be in the environment list provided.", nameof(environmentId));
        }

        var deploymentMaps = GetDeploymentMaps(projectId, environmentId);
        var distinctProjectAndEnvironmentCombinations = GetDistinctProjectAndEnvironmentCombinations(projectId, environmentId);

        return distinctProjectAndEnvironmentCombinations.Select(c =>
            {
                var releasesToKeep = deploymentMaps
                .Where(g => g.Project.Id == c.ProjectId && g.Environment.Id == c.EnvironmentId)
                .OrderByDescending(g => g.DeployedAt)
                .Take(numberOfReleases)
                .Select(g => g.Release)
                .ToList();

                releasesToKeep
                .Select((release, index) => new { Release = release, Index = index })
                .ToList()
                .ForEach(item =>
                    Log.Information(
                        ReasonLog,
                        item.Release.Id,
                        item.Release.Version,
                        c.ProjectName,
                        c.ProjectId,
                        item.Index + 1,
                        c.EnvironmentName,
                        c.EnvironmentId)
                );

                return new ReleaseRetentionResolution(
                    c.ProjectId,
                    c.EnvironmentId,
                    releasesToKeep
                );
            })
            .ToList();
    }

    /// <summary>
    /// Flatten and map all deployments, releases, projects, and environments.
    /// </summary>
    /// <param name="projectId">Optional. Return the maps of the project when provided</param>
    /// <param name="environmentId">Optional. Return the maps of the environment when provided</param>
    /// <returns>A list of deployment map</returns>
    private List<DeploymentMap> GetDeploymentMaps(string? projectId = default, string? environmentId = default)
    {
        return (
            from project in _projects
            where project.Id == projectId || projectId == default
            join release in _releases on project.Id equals release.ProjectId
            join deployment in _deployments on release.Id equals deployment.ReleaseId
            join environment in _environments on deployment.EnvironmentId equals environment.Id
            where environment.Id == environmentId || environmentId == default
            group new { project, release, environment }
            by new
            {
                Project = project,
                Release = release,
                Environment = environment
            }
                into grouped
            select new DeploymentMap(
                grouped.Key.Project,
                grouped.Key.Release,
                grouped.Key.Environment,
                _deployments.Where(d => d.EnvironmentId == grouped.Key.Environment.Id && d.ReleaseId == grouped.Key.Release.Id).Max(d => d.DeployedAt)
            )
        )
        .ToList();
    }

    /// <summary>
    /// Get distinct combination of projects and environments
    /// <param name="projectId">Optional. Return the combination of the project when provided</param>
    /// <param name="environmentId">Optional. Return the combination of the environment when provided</param>
    /// </summary>
    /// <returns>List of combination project id, project name, environment id, and environment name</returns>
    private List<(string ProjectId, string ProjectName, string EnvironmentId, string EnvironmentName)> GetDistinctProjectAndEnvironmentCombinations(string? projectId = default, string? environmentId = default)
    {
        return (
            from project in _projects
            where project.Id == projectId || projectId == default
            from environment in _environments
            where environment.Id == environmentId || environmentId == default
            select new
            {
                ProjectId = project.Id,
                ProjectName = project.Name,
                EnvironmentId = environment.Id,
                EnvironmentName = environment.Name
            }
        )
        .Distinct()
        .Select(c => (c.ProjectId, c.ProjectName, c.EnvironmentId, c.EnvironmentName))
        .ToList();
    }
}
