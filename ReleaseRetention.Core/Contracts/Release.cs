namespace ReleaseRetention.Core.Contracts;

public record Release(string Id, string ProjectId, string? Version, DateTime Created);
