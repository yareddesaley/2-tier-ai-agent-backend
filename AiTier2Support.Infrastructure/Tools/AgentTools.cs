using System.Text.Json;
using AiTier2Support.Application.ReferenceEnvironment;
using AiTier2Support.Application.Tools;

namespace AiTier2Support.Infrastructure.Tools;

internal abstract class BaseTool(IReferenceEnvironment env) : IAgentTool
{
    protected IReferenceEnvironment Env { get; } = env;
    public abstract string Name { get; }
    public abstract string Description { get; }
    public abstract string ParametersJsonSchema { get; }
    public abstract Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken);
}

internal sealed class CheckServiceHealthTool(IReferenceEnvironment env) : BaseTool(env)
{
    public override string Name => "check_service_health";
    public override string Description => "Check current health status of a service (api or worker).";
    public override string ParametersJsonSchema => """{"type":"object","properties":{"service":{"type":"string","description":"Service name: api or worker"}}}""";

    public override Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var service = arguments.TryGetProperty("service", out var s) ? s.GetString() ?? "api" : "api";
        if (service.Equals("worker", StringComparison.OrdinalIgnoreCase))
        {
            var worker = Env.GetWorkerStatus();
            return Task.FromResult(new ToolResult
            {
                Data = new
                {
                    service = "worker",
                    status = worker.Status,
                    queueSize = worker.QueueSize,
                    failedJobs = worker.FailedJobs
                }
            });
        }

        var health = Env.GetServiceHealth(service);
        return Task.FromResult(new ToolResult { Data = health });
    }
}

internal sealed class GetServiceMetricsTool(IReferenceEnvironment env) : BaseTool(env)
{
    public override string Name => "get_service_metrics";
    public override string Description => "Get performance metrics for a service.";
    public override string ParametersJsonSchema => """{"type":"object","properties":{"service":{"type":"string"}}}""";

    public override Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var service = arguments.TryGetProperty("service", out var s) ? s.GetString() ?? "api" : "api";
        return Task.FromResult(new ToolResult { Data = Env.GetServiceMetrics(service) });
    }
}

internal sealed class SearchApplicationLogsTool(IReferenceEnvironment env) : BaseTool(env)
{
    public override string Name => "search_application_logs";
    public override string Description => "Search application logs for error patterns or keywords.";
    public override string ParametersJsonSchema => """{"type":"object","properties":{"query":{"type":"string"},"limit":{"type":"integer"}}}""";

    public override Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var query = arguments.TryGetProperty("query", out var q) ? q.GetString() : null;
        var limit = arguments.TryGetProperty("limit", out var l) ? l.GetInt32() : 50;
        var logs = Env.SearchApplicationLogs(query, limit);
        return Task.FromResult(new ToolResult { Data = new { count = logs.Count, logs } });
    }
}

internal sealed class GetDatabaseMetricsTool(IReferenceEnvironment env) : BaseTool(env)
{
    public override string Name => "get_database_metrics";
    public override string Description => "Get database connection pool and timeout metrics.";
    public override string ParametersJsonSchema => """{"type":"object","properties":{}}""";

    public override Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        Task.FromResult(new ToolResult { Data = Env.GetDatabaseMetrics() });
}

internal sealed class GetRecentDeploymentTool(IReferenceEnvironment env, Application.Github.IGitHubClient gitHub) : BaseTool(env)
{
    public override string Name => "get_recent_deployment";
    public override string Description => "Get the most recent deployment. Uses GitHub if configured, otherwise reference environment.";
    public override string ParametersJsonSchema => """{"type":"object","properties":{}}""";

    public override async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (gitHub.IsConfigured)
        {
            try
            {
                var deployments = await gitHub.GetRecentDeploymentsAsync(1, cancellationToken);
                if (deployments.Count > 0)
                {
                    var d = deployments[0];
                    return new ToolResult
                    {
                        Data = new DeploymentInfo
                        {
                            Id = d.Id, Version = d.Ref, Status = d.State,
                            DeployedAt = d.CreatedAt, CommitSha = d.Sha,
                            Author = d.Creator, Source = "github"
                        }
                    };
                }
            }
            catch { /* fall through */ }
        }

        var deployment = Env.GetRecentDeployment();
        return new ToolResult { Data = deployment };
    }
}

internal sealed class GetDeploymentDetailsTool(IReferenceEnvironment env, Application.Github.IGitHubClient gitHub) : BaseTool(env)
{
    public override string Name => "get_deployment_details";
    public override string Description => "Get details for a specific deployment by ID.";
    public override string ParametersJsonSchema => """{"type":"object","properties":{"deployment_id":{"type":"string"}},"required":["deployment_id"]}""";

    public override async Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetProperty("deployment_id", out var idProp))
            return new ToolResult { Success = false, Error = "deployment_id is required" };

        var deploymentId = idProp.GetString() ?? string.Empty;

        if (gitHub.IsConfigured)
        {
            try
            {
                var d = await gitHub.GetDeploymentAsync(deploymentId, cancellationToken);
                if (d is not null)
                {
                    return new ToolResult
                    {
                        Data = new DeploymentInfo
                        {
                            Id = d.Id, Version = d.Ref, Status = d.State,
                            DeployedAt = d.CreatedAt, CommitSha = d.Sha,
                            Author = d.Creator, Source = "github"
                        }
                    };
                }
            }
            catch { /* fall through */ }
        }

        var deployment = Env.GetDeploymentDetails(deploymentId);
        return deployment is null
            ? new ToolResult { Success = false, Error = $"Deployment {deploymentId} not found" }
            : new ToolResult { Data = deployment };
    }
}

internal sealed class RestartWorkerTool(IReferenceEnvironment env) : BaseTool(env)
{
    public override string Name => "restart_worker";
    public override string Description => "Restart the background worker service. Medium risk action.";
    public override string ParametersJsonSchema => """{"type":"object","properties":{}}""";

    public override Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        Env.RestartWorker();
        return Task.FromResult(new ToolResult { Data = new { status = "restarted", worker = Env.GetWorkerStatus() } });
    }
}

internal sealed class RollbackDeploymentTool(IReferenceEnvironment env) : BaseTool(env)
{
    public override string Name => "rollback_deployment";
    public override string Description => "Rollback a deployment to the previous version. High risk action.";
    public override string ParametersJsonSchema => """{"type":"object","properties":{"deployment_id":{"type":"string"}},"required":["deployment_id"]}""";

    public override Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        if (!arguments.TryGetProperty("deployment_id", out var idProp))
            return Task.FromResult(new ToolResult { Success = false, Error = "deployment_id is required" });

        var deploymentId = idProp.GetString() ?? string.Empty;
        Env.RollbackDeployment(deploymentId);
        return Task.FromResult(new ToolResult
        {
            Data = new { status = "rolled_back", deploymentId, health = Env.GetServiceHealth() }
        });
    }
}

internal sealed class VerifyServiceHealthTool(IReferenceEnvironment env) : BaseTool(env)
{
    public override string Name => "verify_service_health";
    public override string Description => "Verify service health after remediation.";
    public override string ParametersJsonSchema => """{"type":"object","properties":{"action_type":{"type":"string"}}}""";

    public override Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken)
    {
        var actionType = arguments.TryGetProperty("action_type", out var a) ? a.GetString() ?? "unknown" : "unknown";
        var result = Env.VerifyRecovery(actionType);
        return Task.FromResult(new ToolResult { Data = result });
    }
}

internal sealed class SubmitDiagnosisTool : IAgentTool
{
    public string Name => "submit_diagnosis";
    public string Description => "Submit final structured diagnosis after investigation. Call this when you have enough evidence.";
    public string ParametersJsonSchema => """
    {
      "type": "object",
      "properties": {
        "summary": { "type": "string" },
        "rootCause": { "type": "string" },
        "evidence": { "type": "array", "items": { "type": "string" } },
        "alternativeCauses": { "type": "array", "items": { "type": "string" } },
        "confidence": { "type": "number" },
        "recommendedAction": { "type": "string", "enum": ["rollback_deployment", "restart_worker", "escalate"] },
        "riskLevel": { "type": "string", "enum": ["low", "medium", "high"] }
      },
      "required": ["summary", "rootCause", "evidence", "confidence", "recommendedAction", "riskLevel"]
    }
    """;

    public Task<ToolResult> ExecuteAsync(JsonElement arguments, CancellationToken cancellationToken) =>
        Task.FromResult(new ToolResult { Data = JsonSerializer.Deserialize<object>(arguments.GetRawText()) });
}
