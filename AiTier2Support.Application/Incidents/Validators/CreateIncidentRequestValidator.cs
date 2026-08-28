using AiTier2Support.Application.Incidents;
using FluentValidation;

namespace AiTier2Support.Application.Incidents.Validators;

public sealed class CreateIncidentRequestValidator : AbstractValidator<CreateIncidentRequest>
{
    public CreateIncidentRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.ScenarioId).NotEmpty();
    }
}
