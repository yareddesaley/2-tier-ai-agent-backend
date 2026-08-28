using AiTier2Support.Application.ReferenceEnvironment;

namespace AiTier2Support.Infrastructure.ReferenceEnvironment;

internal sealed class ScenarioState
{
    public required ScenarioDefinition Definition { get; init; }
    public required ServiceHealthSnapshot ApiHealth { get; set; }
    public required ServiceMetricsSnapshot ApiMetrics { get; set; }
    public required DatabaseMetricsSnapshot Database { get; set; }
    public required WorkerStatusSnapshot Worker { get; set; }
    public required List<LogEntry> Logs { get; set; }
    public required DeploymentInfo RecentDeployment { get; set; }
    public required ServiceHealthSnapshot HealthyBaseline { get; init; }
    public string RemediationAction { get; init; } = "rollback_deployment";
}

public sealed class ReferenceEnvironment : IReferenceEnvironment
{
    private readonly Dictionary<string, ScenarioState> _scenarios;
    private ScenarioState _active;

    public ReferenceEnvironment()
    {
        _scenarios = BuildScenarios();
        _active = _scenarios["api-latency"];
    }

    public string ActiveScenarioId => _active.Definition.Id;

    public IReadOnlyList<ScenarioDefinition> GetScenarios() =>
        _scenarios.Values.Select(s => s.Definition).ToList();

    public void ResetScenario(string scenarioId)
    {
        if (!_scenarios.TryGetValue(scenarioId, out var template))
            throw new KeyNotFoundException($"Scenario '{scenarioId}' not found.");

        _active = CloneScenario(template);
    }

    public ServiceHealthSnapshot GetServiceHealth(string service = "api") =>
        service.Equals("worker", StringComparison.OrdinalIgnoreCase)
            ? new ServiceHealthSnapshot
            {
                Service = "worker",
                Status = _active.Worker.Status,
                LatencyMs = _active.ApiHealth.LatencyMs,
                ErrorRate = _active.ApiHealth.ErrorRate
            }
            : _active.ApiHealth with { Service = service };

    public ServiceMetricsSnapshot GetServiceMetrics(string service = "api") =>
        _active.ApiMetrics with { Service = service };

    public IReadOnlyList<LogEntry> SearchApplicationLogs(string? query = null, int limit = 50)
    {
        var logs = _active.Logs.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
            logs = logs.Where(l => l.Message.Contains(query, StringComparison.OrdinalIgnoreCase));
        return logs.OrderByDescending(l => l.Timestamp).Take(limit).ToList();
    }

    public DatabaseMetricsSnapshot GetDatabaseMetrics() => _active.Database;

    public WorkerStatusSnapshot GetWorkerStatus() => _active.Worker;

    public DeploymentInfo? GetRecentDeployment() => _active.RecentDeployment;

    public DeploymentInfo? GetDeploymentDetails(string deploymentId) =>
        _active.RecentDeployment.Id == deploymentId ? _active.RecentDeployment : null;

    public void RollbackDeployment(string deploymentId)
    {
        if (_active.RecentDeployment.Id != deploymentId) return;
        ApplyRecovery();
    }

    public void RestartWorker()
    {
        _active.Worker = _active.Worker with { Status = "healthy", QueueSize = 12, FailedJobs = 0 };
        if (_active.RemediationAction == "restart_worker")
            ApplyRecovery();
    }

    public VerificationResult VerifyRecovery(string actionType)
    {
        var before = _active.ApiHealth;
        var after = GetServiceHealth();

        var recovered = actionType == "restart_worker"
            ? _active.Worker.Status == "healthy" && _active.Worker.QueueSize < 100
            : after.Status == "healthy" && after.LatencyMs < 500 && after.ErrorRate < 2;

        return new VerificationResult
        {
            Recovered = recovered,
            Before = before,
            After = after,
            Message = recovered ? "Service recovered successfully." : "Service has not fully recovered."
        };
    }

    private void ApplyRecovery()
    {
        _active.ApiHealth = _active.HealthyBaseline;
        _active.ApiMetrics = new ServiceMetricsSnapshot
        {
            Service = "api",
            LatencyMs = _active.HealthyBaseline.LatencyMs,
            ErrorRate = _active.HealthyBaseline.ErrorRate,
            RequestsPerSecond = 420
        };
        _active.Database = _active.Database with
        {
            ConnectionUsagePercent = 35,
            ActiveConnections = 35,
            ConnectionTimeouts = 0
        };
        _active.Worker = _active.Worker with { Status = "healthy", QueueSize = 12, FailedJobs = 0 };
    }

    private static ScenarioState CloneScenario(ScenarioState template) => new()
    {
        Definition = template.Definition,
        ApiHealth = template.ApiHealth,
        ApiMetrics = template.ApiMetrics,
        Database = template.Database,
        Worker = template.Worker,
        Logs = template.Logs.Select(l => l with { }).ToList(),
        RecentDeployment = template.RecentDeployment,
        HealthyBaseline = template.HealthyBaseline,
        RemediationAction = template.RemediationAction
    };

    private static Dictionary<string, ScenarioState> BuildScenarios()
    {
        var now = DateTime.UtcNow;
        var deployTime = now.AddMinutes(-4);

        var apiLatency = new ScenarioState
        {
            Definition = new ScenarioDefinition
            {
                Id = "api-latency",
                Name = "API Latency",
                Description = "High API latency caused by database connection exhaustion after deployment."
            },
            ApiHealth = new ServiceHealthSnapshot { Service = "api", Status = "degraded", LatencyMs = 2340, ErrorRate = 18 },
            ApiMetrics = new ServiceMetricsSnapshot { Service = "api", LatencyMs = 2340, ErrorRate = 18, RequestsPerSecond = 180 },
            Database = new DatabaseMetricsSnapshot { ConnectionUsagePercent = 98, ActiveConnections = 98, MaxConnections = 100, ConnectionTimeouts = 143 },
            Worker = new WorkerStatusSnapshot { Status = "healthy", QueueSize = 45, FailedJobs = 2 },
            Logs =
            [
                new LogEntry { Timestamp = now.AddMinutes(-3), Level = "ERROR", Message = "Database connection timeout" },
                new LogEntry { Timestamp = now.AddMinutes(-3), Level = "ERROR", Message = "Database connection timeout" },
                new LogEntry { Timestamp = now.AddMinutes(-2), Level = "ERROR", Message = "Database connection timeout" },
                new LogEntry { Timestamp = now.AddMinutes(-2), Level = "WARN", Message = "Request latency exceeded threshold: 2340ms" },
                new LogEntry { Timestamp = now.AddMinutes(-1), Level = "ERROR", Message = "Database connection timeout" }
            ],
            RecentDeployment = new DeploymentInfo
            {
                Id = "892", Version = "v2.14.0", Status = "success",
                DeployedAt = deployTime, CommitSha = "a1b2c3d", Author = "deploy-bot", Source = "reference"
            },
            HealthyBaseline = new ServiceHealthSnapshot { Service = "api", Status = "healthy", LatencyMs = 180, ErrorRate = 0.4 },
            RemediationAction = "rollback_deployment"
        };

        var highErrorRate = new ScenarioState
        {
            Definition = new ScenarioDefinition
            {
                Id = "high-error-rate",
                Name = "High Error Rate",
                Description = "Elevated HTTP 500 errors after a recent deployment."
            },
            ApiHealth = new ServiceHealthSnapshot { Service = "api", Status = "degraded", LatencyMs = 800, ErrorRate = 23 },
            ApiMetrics = new ServiceMetricsSnapshot { Service = "api", LatencyMs = 800, ErrorRate = 23, RequestsPerSecond = 310 },
            Database = new DatabaseMetricsSnapshot { ConnectionUsagePercent = 55, ActiveConnections = 55, MaxConnections = 100, ConnectionTimeouts = 5 },
            Worker = new WorkerStatusSnapshot { Status = "healthy", QueueSize = 30, FailedJobs = 1 },
            Logs =
            [
                new LogEntry { Timestamp = now.AddMinutes(-5), Level = "ERROR", Message = "HTTP 500 Internal Server Error" },
                new LogEntry { Timestamp = now.AddMinutes(-4), Level = "ERROR", Message = "HTTP 500 Internal Server Error" },
                new LogEntry { Timestamp = now.AddMinutes(-3), Level = "ERROR", Message = "HTTP 500 Internal Server Error" },
                new LogEntry { Timestamp = now.AddMinutes(-2), Level = "ERROR", Message = "NullReferenceException in OrderService" }
            ],
            RecentDeployment = new DeploymentInfo
            {
                Id = "891", Version = "v2.13.8", Status = "success",
                DeployedAt = now.AddMinutes(-6), CommitSha = "d4e5f6a", Author = "deploy-bot", Source = "reference"
            },
            HealthyBaseline = new ServiceHealthSnapshot { Service = "api", Status = "healthy", LatencyMs = 195, ErrorRate = 0.3 },
            RemediationAction = "rollback_deployment"
        };

        var workerFailure = new ScenarioState
        {
            Definition = new ScenarioDefinition
            {
                Id = "worker-failure",
                Name = "Background Worker Failure",
                Description = "Background worker unhealthy with growing queue."
            },
            ApiHealth = new ServiceHealthSnapshot { Service = "api", Status = "healthy", LatencyMs = 210, ErrorRate = 1.2 },
            ApiMetrics = new ServiceMetricsSnapshot { Service = "api", LatencyMs = 210, ErrorRate = 1.2, RequestsPerSecond = 390 },
            Database = new DatabaseMetricsSnapshot { ConnectionUsagePercent = 40, ActiveConnections = 40, MaxConnections = 100, ConnectionTimeouts = 0 },
            Worker = new WorkerStatusSnapshot { Status = "unhealthy", QueueSize = 1250, FailedJobs = 86 },
            Logs =
            [
                new LogEntry { Timestamp = now.AddMinutes(-10), Level = "ERROR", Message = "Worker process crashed: OutOfMemoryException" },
                new LogEntry { Timestamp = now.AddMinutes(-8), Level = "WARN", Message = "Job retry limit exceeded" },
                new LogEntry { Timestamp = now.AddMinutes(-5), Level = "ERROR", Message = "Queue backlog exceeds threshold: 1250" }
            ],
            RecentDeployment = new DeploymentInfo
            {
                Id = "890", Version = "v2.13.5", Status = "success",
                DeployedAt = now.AddHours(-2), CommitSha = "b7c8d9e", Author = "deploy-bot", Source = "reference"
            },
            HealthyBaseline = new ServiceHealthSnapshot { Service = "api", Status = "healthy", LatencyMs = 210, ErrorRate = 1.2 },
            RemediationAction = "restart_worker"
        };

        var dbExhaustion = new ScenarioState
        {
            Definition = new ScenarioDefinition
            {
                Id = "db-exhaustion",
                Name = "Database Connection Exhaustion",
                Description = "Database connections at 99% with high timeouts."
            },
            ApiHealth = new ServiceHealthSnapshot { Service = "api", Status = "degraded", LatencyMs = 1890, ErrorRate = 15 },
            ApiMetrics = new ServiceMetricsSnapshot { Service = "api", LatencyMs = 1890, ErrorRate = 15, RequestsPerSecond = 200 },
            Database = new DatabaseMetricsSnapshot { ConnectionUsagePercent = 99, ActiveConnections = 99, MaxConnections = 100, ConnectionTimeouts = 210 },
            Worker = new WorkerStatusSnapshot { Status = "degraded", QueueSize = 340, FailedJobs = 24 },
            Logs =
            [
                new LogEntry { Timestamp = now.AddMinutes(-4), Level = "ERROR", Message = "Database connection timeout" },
                new LogEntry { Timestamp = now.AddMinutes(-3), Level = "ERROR", Message = "Connection pool exhausted" },
                new LogEntry { Timestamp = now.AddMinutes(-2), Level = "WARN", Message = "Worker holding stale connections" }
            ],
            RecentDeployment = new DeploymentInfo
            {
                Id = "889", Version = "v2.13.2", Status = "success",
                DeployedAt = now.AddHours(-5), CommitSha = "f1a2b3c", Author = "deploy-bot", Source = "reference"
            },
            HealthyBaseline = new ServiceHealthSnapshot { Service = "api", Status = "healthy", LatencyMs = 175, ErrorRate = 0.5 },
            RemediationAction = "restart_worker"
        };

        return new Dictionary<string, ScenarioState>
        {
            [apiLatency.Definition.Id] = apiLatency,
            [highErrorRate.Definition.Id] = highErrorRate,
            [workerFailure.Definition.Id] = workerFailure,
            [dbExhaustion.Definition.Id] = dbExhaustion
        };
    }
}
