using AiTier2Support.Domain.Actions;
using AiTier2Support.Domain.Agents;
using AiTier2Support.Domain.Approvals;
using AiTier2Support.Domain.Common;

namespace AiTier2Support.Domain.Incidents;

public class Incident : Entity
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IncidentSeverity Severity { get; set; }
    public IncidentStatus Status { get; set; } = IncidentStatus.Open;
    public string ScenarioId { get; set; } = string.Empty;
    public string? RootCause { get; set; }
    public string? RecommendedAction { get; set; }
    public double? Confidence { get; set; }
    public RiskLevel? RiskLevel { get; set; }
    public string? DiagnosisJson { get; set; }
    public string? EscalationReason { get; set; }
    public string? EscalationNextStep { get; set; }

    public ICollection<AgentRun> AgentRuns { get; set; } = [];
    public ICollection<Evidence> Evidence { get; set; } = [];
    public ICollection<AgentAction> Actions { get; set; } = [];
    public IncidentReport? Report { get; set; }
}

public class AgentRun : Entity
{
    public Guid IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;
    public AgentRunStatus Status { get; set; } = AgentRunStatus.Running;
    public int IterationCount { get; set; }
    public string? FailureReason { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<AgentMessage> Messages { get; set; } = [];
    public ICollection<ToolExecution> ToolExecutions { get; set; } = [];
}

public class AgentMessage : Entity
{
    public Guid AgentRunId { get; set; }
    public AgentRun AgentRun { get; set; } = null!;
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Sequence { get; set; }
}

public class ToolExecution : Entity
{
    public Guid AgentRunId { get; set; }
    public AgentRun AgentRun { get; set; } = null!;
    public string ToolName { get; set; } = string.Empty;
    public string ArgumentsJson { get; set; } = "{}";
    public string ResultJson { get; set; } = "{}";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int Sequence { get; set; }
}

public class Evidence : Entity
{
    public Guid IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;
    public string Source { get; set; } = string.Empty;
    public string Tool { get; set; } = string.Empty;
    public string Observation { get; set; } = string.Empty;
    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;
}

public class AgentAction : Entity
{
    public Guid IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;
    public string ActionType { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public RiskLevel RiskLevel { get; set; }
    public double Confidence { get; set; }
    public AgentActionStatus Status { get; set; } = AgentActionStatus.Pending;
    public string? ParametersJson { get; set; }
    public string? ResultJson { get; set; }
    public string? VerificationJson { get; set; }
    public DateTime? ExecutedAt { get; set; }

    public ApprovalRequest? ApprovalRequest { get; set; }
}

public class ApprovalRequest : Entity
{
    public Guid AgentActionId { get; set; }
    public AgentAction AgentAction { get; set; } = null!;
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}

public class IncidentReport : Entity
{
    public Guid IncidentId { get; set; }
    public Incident Incident { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
