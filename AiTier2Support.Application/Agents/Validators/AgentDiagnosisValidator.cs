using AiTier2Support.Application.Agents;
using FluentValidation;

namespace AiTier2Support.Application.Agents.Validators;

public sealed class AgentDiagnosisValidator : AbstractValidator<AgentDiagnosis>
{
    public AgentDiagnosisValidator()
    {
        RuleFor(x => x.Summary).NotEmpty();
        RuleFor(x => x.RootCause).NotEmpty();
        RuleFor(x => x.Confidence).InclusiveBetween(0, 1);
        RuleFor(x => x.RecommendedAction).NotEmpty();
        RuleFor(x => x.Evidence).NotEmpty();
    }
}
