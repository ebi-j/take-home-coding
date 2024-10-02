namespace ReleaseRetention.Core.Contracts;

public record Deployment(string Id, string ReleaseId, string EnvironmentId, DateTime DeployedAt);
