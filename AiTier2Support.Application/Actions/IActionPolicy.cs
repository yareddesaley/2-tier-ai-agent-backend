using AiTier2Support.Domain.Actions;

namespace AiTier2Support.Application.Actions;

public interface IActionPolicy
{
    RiskLevel GetRiskLevel(string actionType);
    bool CanExecuteAutomatically(string actionType);
    bool RequiresApproval(string actionType);
}

public interface IRiskPolicy
{
    RiskLevel GetToolRiskLevel(string toolName);
}
