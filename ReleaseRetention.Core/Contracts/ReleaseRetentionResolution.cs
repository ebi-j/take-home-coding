namespace ReleaseRetention.Core.Contracts;

public record ReleaseRetentionResolution(string ProjectId, string EnvironmentId, List<Release> ReleasesToKeep);
