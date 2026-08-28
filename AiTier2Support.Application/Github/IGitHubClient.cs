namespace AiTier2Support.Application.Github;

public sealed class GitHubDeployment
{
    public string Id { get; init; } = string.Empty;
    public string Sha { get; init; } = string.Empty;
    public string Ref { get; init; } = string.Empty;
    public string Environment { get; init; } = "production";
    public string State { get; init; } = "success";
    public DateTime CreatedAt { get; init; }
    public string? Creator { get; init; }
    public string? Description { get; init; }
}

public interface IGitHubClient
{
    bool IsConfigured { get; }
    Task<IReadOnlyList<GitHubDeployment>> GetRecentDeploymentsAsync(int count, CancellationToken cancellationToken);
    Task<GitHubDeployment?> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken);
}
