using System.Text.Json;
using AiTier2Support.Application.Actions;
using AiTier2Support.Application.Agents;
using AiTier2Support.Application.Agents.Validators;
using AiTier2Support.Application.Ai;
using AiTier2Support.Application.ReferenceEnvironment;
using AiTier2Support.Application.Tools;
using AiTier2Support.Domain.Actions;
using AiTier2Support.Domain.Agents;
using AiTier2Support.Domain.Approvals;
using AiTier2Support.Domain.Incidents;
using AiTier2Support.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiTier2Support.Infrastructure.Agents;

public sealed class AgentOrchestrator : IAgentOrchestrator
{
    private const int MaxAgentIterations = 10;
    private const double MinConfidenceThreshold = 0.5;

    private readonly AppDbContext _db;
    private readonly ILlmClient _llm;
    private readonly IAgentToolRegistry _toolRegistry;
    private readonly IActionPolicy _actionPolicy;
    private readonly IReferenceEnvironment _environment;
    private readonly IValidator<AgentDiagnosis> _diagnosisValidator;
    private readonly ILogger<AgentOrchestrator> _logger;

    public AgentOrchestrator(
        AppDbContext db,
        ILlmClient llm,
        IAgentToolRegistry toolRegistry,
        IActionPolicy actionPolicy,
        IReferenceEnvironment environment,
        IValidator<AgentDiagnosis> diagnosisValidator,
        ILogger<AgentOrchestrator> logger)
    {
        _db = db;
        _llm = llm;
        _toolRegistry = toolRegistry;
        _actionPolicy = actionPolicy;
        _environment = environment;
        _diagnosisValidator = diagnosisValidator;
        _logger = logger;
    }

    public async Task<AgentInvestigationResult> InvestigateAsync(Guid incidentId, CancellationToken cancellationToken)
    {
        var incident = await _db.GetIncidentAsync(incidentId, cancellationToken)
            ?? throw new KeyNotFoundException($"Incident {incidentId} not found.");

        _environment.ResetScenario(incident.ScenarioId);
        if (incident.Status == IncidentStatus.Open)
            IncidentStateMachine.Transition(incident, IncidentStatus.Investigating);

        var agentRun = new AgentRun { IncidentId = incidentId, Status = AgentRunStatus.Running };
        _db.AgentRuns.Add(agentRun);
        await _db.SaveChangesAsync(cancellationToken);

        var messages = new List<LlmMessage>
        {
            new()
            {
                Role = "user",
                Content = $"""
                    Investigate this incident:
                    Title: {incident.Title}
                    Description: {incident.Description}
                    Severity: {incident.Severity}
                    Scenario: {incident.ScenarioId}

                    Use available tools to gather evidence. When you have sufficient evidence, call submit_diagnosis with your structured diagnosis.
                    Recommended actions must be one of: rollback_deployment, restart_worker, escalate.
                    """
            }
        };

        var investigationTools = _toolRegistry.GetAllTools()
            .Where(t => t.Name != "verify_service_health")
            .ToList();

        AgentDiagnosis? diagnosis = null;
        var toolSequence = 0;

        try
        {
            for (var i = 0; i < MaxAgentIterations; i++)
            {
                agentRun.IterationCount = i + 1;
                var response = await _llm.GenerateAsync(new LlmRequest
                {
                    SystemPrompt = BuildSystemPrompt(),
                    Messages = messages,
                    Tools = investigationTools.Select(t => new LlmToolDefinition
                    {
                        Name = t.Name,
                        Description = t.Description,
                        ParametersJsonSchema = t.ParametersJsonSchema
                    }).ToList()
                }, cancellationToken);

                if (response.ToolCalls.Count == 0)
                {
                    if (!string.IsNullOrWhiteSpace(response.Text))
                    {
                        messages.Add(new LlmMessage { Role = "assistant", Content = response.Text });
                    }
                    break;
                }

                var assistantMsg = new LlmMessage { Role = "assistant", ToolCalls = response.ToolCalls, Content = response.Text ?? string.Empty };
                messages.Add(assistantMsg);
                _db.AgentMessages.Add(new AgentMessage
                {
                    AgentRunId = agentRun.Id, Role = "assistant",
                    Content = response.Text ?? JsonSerializer.Serialize(response.ToolCalls),
                    Sequence = messages.Count
                });

                foreach (var toolCall in response.ToolCalls)
                {
                    toolSequence++;
                    var tool = _toolRegistry.GetTool(toolCall.Name);
                    if (tool is null)
                    {
                        await RecordToolExecution(agentRun, toolCall, toolSequence, false, "{}", "Unknown tool", cancellationToken);
                        continue;
                    }

                    using var argsDoc = JsonDocument.Parse(toolCall.ArgumentsJson);
                    var result = await tool.ExecuteAsync(argsDoc.RootElement, cancellationToken);
                    var resultJson = result.ToJson();

                    await RecordToolExecution(agentRun, toolCall, toolSequence, result.Success, toolCall.ArgumentsJson, resultJson, cancellationToken, result.Error);
                    await CollectEvidence(incident, toolCall.Name, result, cancellationToken);

                    if (toolCall.Name == "submit_diagnosis" && result.Success)
                    {
                        diagnosis = ParseDiagnosis(argsDoc.RootElement);
                        if (diagnosis is not null)
                        {
                            var validation = await _diagnosisValidator.ValidateAsync(diagnosis, cancellationToken);
                            if (!validation.IsValid)
                            {
                                diagnosis = null;
                                messages.Add(new LlmMessage
                                {
                                    Role = "tool", ToolName = toolCall.Name, ToolCallId = toolCall.Id,
                                    Content = JsonSerializer.Serialize(new { error = "Invalid diagnosis", details = validation.Errors.Select(e => e.ErrorMessage) })
                                });
                                continue;
                            }
                        }
                    }

                    messages.Add(new LlmMessage
                    {
                        Role = "tool", ToolName = toolCall.Name, ToolCallId = toolCall.Id, Content = resultJson
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);

                if (diagnosis is not null) break;
            }

            if (diagnosis is null)
            {
                diagnosis = await RetryDiagnosisAsync(incident, messages, cancellationToken);
            }

            if (diagnosis is null || diagnosis.Confidence < MinConfidenceThreshold)
            {
                return await EscalateAsync(incident, agentRun,
                    "Insufficient confidence or invalid diagnosis from AI agent.",
                    "Manual investigation by senior engineer required.", cancellationToken);
            }

            if (diagnosis.RecommendedAction.Equals("escalate", StringComparison.OrdinalIgnoreCase))
            {
                return await EscalateAsync(incident, agentRun, diagnosis.RootCause,
                    "Escalate to platform team for manual remediation.", cancellationToken);
            }

            incident.DiagnosisJson = JsonSerializer.Serialize(diagnosis);
            incident.RootCause = diagnosis.RootCause;
            incident.Confidence = diagnosis.Confidence;
            incident.RecommendedAction = diagnosis.RecommendedAction;
            incident.RiskLevel = ParseRiskLevel(diagnosis.RiskLevel);

            var action = await CreateRemediationAction(incident, diagnosis, cancellationToken);

            if (_actionPolicy.RequiresApproval(action.ActionType))
            {
                IncidentStateMachine.Transition(incident, IncidentStatus.AwaitingApproval);
                action.Status = AgentActionStatus.AwaitingApproval;
                var approval = new ApprovalRequest { AgentActionId = action.Id, Status = ApprovalStatus.Pending };
                _db.ApprovalRequests.Add(approval);
                agentRun.Status = AgentRunStatus.Completed;
                agentRun.CompletedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);

                return new AgentInvestigationResult
                {
                    AgentRunId = agentRun.Id,
                    Status = "AwaitingApproval",
                    Diagnosis = diagnosis,
                    RequiresApproval = true,
                    PendingActionId = action.Id
                };
            }

            return await ExecuteAndVerifyAsync(incident, agentRun, action, diagnosis, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Agent investigation failed for incident {IncidentId}", incidentId);
            agentRun.Status = AgentRunStatus.Failed;
            agentRun.FailureReason = ex.Message;
            agentRun.CompletedAt = DateTime.UtcNow;
            IncidentStateMachine.Transition(incident, IncidentStatus.Escalated);
            incident.EscalationReason = "Agent investigation failed.";
            incident.EscalationNextStep = "Review agent logs and retry manually.";
            await _db.SaveChangesAsync(cancellationToken);

            return new AgentInvestigationResult
            {
                AgentRunId = agentRun.Id,
                Status = "Failed"
            };
        }
    }

    public async Task ContinueAfterApprovalAsync(Guid actionId, bool approved, string? reviewer, CancellationToken cancellationToken)
    {
        var action = await _db.AgentActions
            .Include(a => a.ApprovalRequest)
            .Include(a => a.Incident)
            .FirstOrDefaultAsync(a => a.Id == actionId, cancellationToken)
            ?? throw new KeyNotFoundException($"Action {actionId} not found.");

        if (action.ApprovalRequest is null)
            throw new InvalidOperationException("Action does not require approval.");

        if (!approved)
        {
            action.ApprovalRequest.Status = ApprovalStatus.Rejected;
            action.ApprovalRequest.ReviewedBy = reviewer;
            action.ApprovalRequest.ReviewedAt = DateTime.UtcNow;
            action.Status = AgentActionStatus.Rejected;
            IncidentStateMachine.Transition(action.Incident, IncidentStatus.Escalated);
            action.Incident.EscalationReason = "Remediation action rejected by operator.";
            action.Incident.EscalationNextStep = "Perform manual remediation or re-investigate.";
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        action.ApprovalRequest.Status = ApprovalStatus.Approved;
        action.ApprovalRequest.ReviewedBy = reviewer;
        action.ApprovalRequest.ReviewedAt = DateTime.UtcNow;
        action.Status = AgentActionStatus.Approved;

        var agentRun = action.Incident.AgentRuns.OrderByDescending(r => r.CreatedAt).FirstOrDefault()
            ?? new AgentRun { IncidentId = action.IncidentId, Status = AgentRunStatus.Running };

        var diagnosis = ParseDiagnosisFromJson(action.Incident.DiagnosisJson);
        await ExecuteAndVerifyAsync(action.Incident, agentRun, action, diagnosis, cancellationToken);
    }

    private async Task<AgentInvestigationResult> ExecuteAndVerifyAsync(
        Incident incident, AgentRun agentRun, AgentAction action,
        AgentDiagnosis? diagnosis, CancellationToken cancellationToken)
    {
        IncidentStateMachine.Transition(incident, IncidentStatus.Remediating);
        action.Status = AgentActionStatus.Executing;
        await _db.SaveChangesAsync(cancellationToken);

        var tool = _toolRegistry.GetTool(action.ActionType)
            ?? throw new InvalidOperationException($"Tool {action.ActionType} not found.");

        using var argsDoc = string.IsNullOrWhiteSpace(action.ParametersJson)
            ? JsonDocument.Parse("{}")
            : JsonDocument.Parse(action.ParametersJson);

        var beforeHealth = _environment.GetServiceHealth();
        var execResult = await tool.ExecuteAsync(argsDoc.RootElement, cancellationToken);
        action.ResultJson = execResult.ToJson();
        action.ExecutedAt = DateTime.UtcNow;
        action.Status = execResult.Success ? AgentActionStatus.Completed : AgentActionStatus.Failed;
        await _db.SaveChangesAsync(cancellationToken);

        IncidentStateMachine.Transition(incident, IncidentStatus.Verifying);
        var verifyTool = _toolRegistry.GetTool("verify_service_health")!;
        using var verifyArgs = JsonDocument.Parse(JsonSerializer.Serialize(new { action_type = action.ActionType }));
        var verifyResult = await verifyTool.ExecuteAsync(verifyArgs.RootElement, cancellationToken);
        action.VerificationJson = verifyResult.ToJson();
        await _db.SaveChangesAsync(cancellationToken);

        var verification = JsonSerializer.Deserialize<VerificationResult>(verifyResult.ToJson(),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (verification?.Recovered == true)
        {
            IncidentStateMachine.Transition(incident, IncidentStatus.Resolved);
            agentRun.Status = AgentRunStatus.Completed;
            agentRun.CompletedAt = DateTime.UtcNow;
            await GenerateReportAsync(incident, diagnosis, action, verification, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);

            return new AgentInvestigationResult
            {
                AgentRunId = agentRun.Id,
                Status = "Resolved",
                Diagnosis = diagnosis
            };
        }

        return await EscalateAsync(incident, agentRun,
            "Remediation did not restore service health.",
            $"Verification failed. Before: {beforeHealth.LatencyMs}ms/{beforeHealth.Status}, After: {verification?.After.LatencyMs}ms/{verification?.After.Status}",
            cancellationToken);
    }

    private async Task<AgentAction> CreateRemediationAction(Incident incident, AgentDiagnosis diagnosis, CancellationToken cancellationToken)
    {
        var deployment = _environment.GetRecentDeployment();
        var parameters = diagnosis.RecommendedAction == "rollback_deployment" && deployment is not null
            ? JsonSerializer.Serialize(new { deployment_id = deployment.Id })
            : "{}";

        var action = new AgentAction
        {
            IncidentId = incident.Id,
            ActionType = diagnosis.RecommendedAction,
            Reason = diagnosis.Summary,
            RiskLevel = _actionPolicy.GetRiskLevel(diagnosis.RecommendedAction),
            Confidence = diagnosis.Confidence,
            ParametersJson = parameters,
            Status = AgentActionStatus.Pending
        };

        _db.AgentActions.Add(action);
        await _db.SaveChangesAsync(cancellationToken);
        return action;
    }

    private async Task GenerateReportAsync(
        Incident incident, AgentDiagnosis? diagnosis, AgentAction action,
        VerificationResult verification, CancellationToken cancellationToken)
    {
        try
        {
            var evidenceList = incident.Evidence.Select(e => $"- {e.Observation}").ToList();
            var reportPrompt = $"""
                Generate a concise incident report for:
                Incident: {incident.Title}
                Root Cause: {diagnosis?.RootCause ?? incident.RootCause}
                Action: {action.ActionType}
                Result: {(verification.Recovered ? "Recovered" : "Not recovered")}
                Before: latency={verification.Before.LatencyMs}ms, status={verification.Before.Status}
                After: latency={verification.After.LatencyMs}ms, status={verification.After.Status}
                Evidence:
                {string.Join("\n", evidenceList)}
                """;

            var response = await _llm.GenerateAsync(new LlmRequest
            {
                SystemPrompt = "You are an incident report writer. Produce a concise, professional post-incident report.",
                Messages = [new LlmMessage { Role = "user", Content = reportPrompt }]
            }, cancellationToken);

            var content = response.Text ?? BuildFallbackReport(incident, diagnosis, action, verification);
            incident.Report = new IncidentReport { IncidentId = incident.Id, Content = content };
        }
        catch
        {
            incident.Report = new IncidentReport
            {
                IncidentId = incident.Id,
                Content = BuildFallbackReport(incident, diagnosis, action, verification)
            };
        }
    }

    private static string BuildFallbackReport(Incident incident, AgentDiagnosis? diagnosis, AgentAction action, VerificationResult verification) =>
        $"""
        Incident: {incident.Title}

        Impact: {incident.Description}

        Root Cause: {diagnosis?.RootCause ?? incident.RootCause ?? "Unknown"}

        Evidence:
        {string.Join("\n", incident.Evidence.Select(e => $"- {e.Observation}"))}

        Action: {action.ActionType} executed.

        Result: {(verification.Recovered ? "Service recovered." : "Recovery failed.")}
        Before: {verification.Before.LatencyMs}ms / {verification.Before.Status}
        After: {verification.After.LatencyMs}ms / {verification.After.Status}

        Confidence: {(diagnosis?.Confidence ?? incident.Confidence ?? 0) * 100:F0}%
        """;

    private async Task<AgentInvestigationResult> EscalateAsync(
        Incident incident, AgentRun agentRun, string reason, string nextStep, CancellationToken cancellationToken)
    {
        IncidentStateMachine.Transition(incident, IncidentStatus.Escalated);
        incident.EscalationReason = reason;
        incident.EscalationNextStep = nextStep;
        agentRun.Status = AgentRunStatus.Escalated;
        agentRun.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new AgentInvestigationResult
        {
            AgentRunId = agentRun.Id,
            Status = "Escalated"
        };
    }

    private async Task<AgentDiagnosis?> RetryDiagnosisAsync(Incident incident, List<LlmMessage> messages, CancellationToken cancellationToken)
    {
        try
        {
            var submitDiagnosisTool = _toolRegistry.GetTool("submit_diagnosis")!;
            var response = await _llm.GenerateAsync(new LlmRequest
            {
                SystemPrompt = BuildSystemPrompt(),
                Messages = messages.Concat([
                    new LlmMessage
                    {
                        Role = "user",
                        Content = "Provide your final diagnosis now using submit_diagnosis tool."
                    }
                ]).ToList(),
                Tools =
                [
                    new LlmToolDefinition
                    {
                        Name = submitDiagnosisTool.Name,
                        Description = submitDiagnosisTool.Description,
                        ParametersJsonSchema = submitDiagnosisTool.ParametersJsonSchema
                    }
                ]
            }, cancellationToken);

            var call = response.ToolCalls.FirstOrDefault(c => c.Name == "submit_diagnosis");
            if (call is null) return null;
            using var doc = JsonDocument.Parse(call.ArgumentsJson);
            return ParseDiagnosis(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    private async Task RecordToolExecution(
        AgentRun run, LlmToolCall call, int sequence, bool success,
        string argsJson, string resultJson, CancellationToken ct, string? error = null)
    {
        _db.ToolExecutions.Add(new ToolExecution
        {
            AgentRunId = run.Id,
            ToolName = call.Name,
            ArgumentsJson = argsJson,
            ResultJson = resultJson,
            Success = success,
            ErrorMessage = error,
            Sequence = sequence
        });
        await _db.SaveChangesAsync(ct);
    }

    private async Task CollectEvidence(Incident incident, string toolName, ToolResult result, CancellationToken ct)
    {
        if (!result.Success || result.Data is null) return;

        var observations = ExtractObservations(toolName, result.Data);
        foreach (var obs in observations)
        {
            _db.Evidence.Add(new Evidence
            {
                IncidentId = incident.Id,
                Source = obs.Source,
                Tool = toolName,
                Observation = obs.Observation,
                ObservedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync(ct);
    }

    private static IEnumerable<(string Source, string Observation)> ExtractObservations(string toolName, object data)
    {
        var json = JsonSerializer.Serialize(data);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        return toolName switch
        {
            "check_service_health" => [("API Health", $"status={GetString(root,"status")}, latency={GetDouble(root,"latencyMs")}ms, errorRate={GetDouble(root,"errorRate")}%")],
            "get_service_metrics" => [("Service Metrics", $"latency={GetDouble(root,"latencyMs")}ms, errorRate={GetDouble(root,"errorRate")}%, rps={GetDouble(root,"requestsPerSecond")}")],
            "search_application_logs" => root.TryGetProperty("logs", out var logs)
                ? logs.EnumerateArray().Take(3).Select(l => ("Application Logs", l.GetProperty("message").GetString() ?? ""))
                : [("Application Logs", "No matching logs")],
            "get_database_metrics" => [("Database", $"connection usage={GetDouble(root,"connectionUsagePercent")}%, timeouts={GetInt(root,"connectionTimeouts")}")],
            "get_recent_deployment" or "get_deployment_details" => [("Deployment", $"deployment #{GetString(root,"id")} at {GetString(root,"deployedAt")} ({GetString(root,"version")})")],
            "get_worker_status" or _ when root.TryGetProperty("status", out _) && root.TryGetProperty("queueSize", out _) =>
                [("Worker", $"status={GetString(root,"status")}, queue={GetInt(root,"queueSize")}, failed={GetInt(root,"failedJobs")}")],
            _ => []
        };
    }

    private static AgentDiagnosis? ParseDiagnosis(JsonElement el) => new()
    {
        Summary = el.GetProperty("summary").GetString() ?? "",
        RootCause = el.GetProperty("rootCause").GetString() ?? "",
        Evidence = el.TryGetProperty("evidence", out var ev)
            ? ev.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [],
        AlternativeCauses = el.TryGetProperty("alternativeCauses", out var alt)
            ? alt.EnumerateArray().Select(x => x.GetString() ?? "").ToList() : [],
        Confidence = el.GetProperty("confidence").GetDouble(),
        RecommendedAction = el.GetProperty("recommendedAction").GetString() ?? "escalate",
        RiskLevel = el.GetProperty("riskLevel").GetString() ?? "high"
    };

    private static AgentDiagnosis? ParseDiagnosisFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        return JsonSerializer.Deserialize<AgentDiagnosis>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    private static RiskLevel ParseRiskLevel(string risk) => risk.ToLowerInvariant() switch
    {
        "low" => Domain.Actions.RiskLevel.Low,
        "medium" => Domain.Actions.RiskLevel.Medium,
        _ => Domain.Actions.RiskLevel.High
    };

    private static string GetString(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) ? v.ToString() : "n/a";

    private static double GetDouble(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.TryGetDouble(out var d) ? d : 0;

    private static int GetInt(JsonElement el, string prop) =>
        el.TryGetProperty(prop, out var v) && v.TryGetInt32(out var i) ? i : 0;

    private static string BuildSystemPrompt() => """
        You are a Tier-2 incident response engineer for a SaaS platform.
        Investigate incidents systematically using available tools.
        Gather evidence from service health, metrics, logs, database, deployments, and workers.
        When you have enough evidence, call submit_diagnosis with structured output.
        Do not guess - use tool results as evidence.
        For database connection exhaustion near a recent deployment, recommend rollback_deployment.
        For worker failures with large queue backlogs, recommend restart_worker.
        Set riskLevel appropriately: rollback_deployment=high, restart_worker=medium.
        Do not expose chain-of-thought. Only use registered tools.
        """;
}
