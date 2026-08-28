namespace AiTier2Support.Domain.Actions;

public enum AgentActionStatus
{
    Pending = 0,
    AwaitingApproval = 1,
    Approved = 2,
    Rejected = 3,
    Executing = 4,
    Completed = 5,
    Failed = 6
}
