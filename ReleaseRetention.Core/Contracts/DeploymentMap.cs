namespace ReleaseRetention.Core.Contracts;

/// <summary>
/// A map to list all the release and deployment information
/// </summary>
public record DeploymentMap(Project Project, Release Release, DeploymentEnvironment Environment, DateTime DeployedAt);
