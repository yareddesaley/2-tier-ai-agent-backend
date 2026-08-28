using AiTier2Support.Domain.Actions;

namespace AiTier2Support.Application.Actions;

public sealed class ActionPolicy : IActionPolicy, IRiskPolicy
{
    private static readonly Dictionary<string, RiskLevel> ActionRisks = new(StringComparer.OrdinalIgnoreCase)
    {
        ["check_service_health"] = RiskLevel.Low,
        ["get_service_metrics"] = RiskLevel.Low,
        ["search_application_logs"] = RiskLevel.Low,
        ["get_database_metrics"] = RiskLevel.Low,
        ["get_recent_deployment"] = RiskLevel.Low,
        ["get_deployment_details"] = RiskLevel.Low,
        ["verify_service_health"] = RiskLevel.Low,
        ["restart_worker"] = RiskLevel.Medium,
        ["rollback_deployment"] = RiskLevel.High,
        ["submit_diagnosis"] = RiskLevel.Low
    };

    public RiskLevel GetRiskLevel(string actionType) =>
        ActionRisks.TryGetValue(actionType, out var risk) ? risk : RiskLevel.High;

    public RiskLevel GetToolRiskLevel(string toolName) => GetRiskLevel(toolName);

    public bool CanExecuteAutomatically(string actionType) =>
        GetRiskLevel(actionType) == RiskLevel.Low;

    public bool RequiresApproval(string actionType) =>
        GetRiskLevel(actionType) == RiskLevel.High;
}
