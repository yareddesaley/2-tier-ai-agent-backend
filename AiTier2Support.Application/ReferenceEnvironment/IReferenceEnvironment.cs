namespace AiTier2Support.Application.ReferenceEnvironment;

public sealed class ScenarioDefinition
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed record ServiceHealthSnapshot
{
    public string Service { get; init; } = "api";
    public string Status { get; init; } = "healthy";
    public double LatencyMs { get; init; }
    public double ErrorRate { get; init; }
}

public sealed record ServiceMetricsSnapshot
{
    public string Service { get; init; } = "api";
    public double LatencyMs { get; init; }
    public double ErrorRate { get; init; }
    public double RequestsPerSecond { get; init; }
}

public sealed record LogEntry
{
    public DateTime Timestamp { get; init; }
    public string Level { get; init; } = "INFO";
    public string Message { get; init; } = string.Empty;
}

public sealed record DatabaseMetricsSnapshot
{
    public double ConnectionUsagePercent { get; init; }
    public int ActiveConnections { get; init; }
    public int MaxConnections { get; init; }
    public int ConnectionTimeouts { get; init; }
}

public sealed record WorkerStatusSnapshot
{
    public string Status { get; init; } = "healthy";
    public int QueueSize { get; init; }
    public int FailedJobs { get; init; }
}

public sealed class DeploymentInfo
{
    public string Id { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string Status { get; init; } = "success";
    public DateTime DeployedAt { get; init; }
    public string? CommitSha { get; init; }
    public string? Author { get; init; }
    public string Source { get; init; } = "reference";
}

public interface IReferenceEnvironment
{
    string ActiveScenarioId { get; }
    IReadOnlyList<ScenarioDefinition> GetScenarios();
    void ResetScenario(string scenarioId);
    ServiceHealthSnapshot GetServiceHealth(string service = "api");
    ServiceMetricsSnapshot GetServiceMetrics(string service = "api");
    IReadOnlyList<LogEntry> SearchApplicationLogs(string? query = null, int limit = 50);
    DatabaseMetricsSnapshot GetDatabaseMetrics();
    WorkerStatusSnapshot GetWorkerStatus();
    DeploymentInfo? GetRecentDeployment();
    DeploymentInfo? GetDeploymentDetails(string deploymentId);
    void RollbackDeployment(string deploymentId);
    void RestartWorker();
    VerificationResult VerifyRecovery(string actionType);
}

public sealed class VerificationResult
{
    public bool Recovered { get; init; }
    public ServiceHealthSnapshot Before { get; init; } = new();
    public ServiceHealthSnapshot After { get; init; } = new();
    public string Message { get; init; } = string.Empty;
}
