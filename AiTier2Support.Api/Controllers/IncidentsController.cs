using AiTier2Support.Application.Incidents;
using AiTier2Support.Application.ReferenceEnvironment;
using AiTier2Support.Domain.Incidents;
using Microsoft.AspNetCore.Mvc;

namespace AiTier2Support.Api.Controllers;

[ApiController]
[Route("api/incidents")]
public sealed class IncidentsController : ControllerBase
{
    private readonly IIncidentService _incidents;
    private readonly IInvestigationService _investigation;

    public IncidentsController(IIncidentService incidents, IInvestigationService investigation)
    {
        _incidents = incidents;
        _investigation = investigation;
    }

    [HttpPost]
    public async Task<ActionResult<IncidentSummaryDto>> Create([FromBody] CreateIncidentRequest request, CancellationToken ct)
    {
        var result = await _incidents.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IncidentSummaryDto>>> GetAll(CancellationToken ct) =>
        Ok(await _incidents.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IncidentDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var incident = await _incidents.GetByIdAsync(id, ct);
        return incident is null ? NotFound() : Ok(incident);
    }

    [HttpPost("{id:guid}/investigate")]
    public async Task<ActionResult> Investigate(Guid id, CancellationToken ct)
    {
        var result = await _investigation.StartInvestigationAsync(id, ct);
        return Accepted(result);
    }

    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult> GetTimeline(Guid id, CancellationToken ct) =>
        Ok(await _incidents.GetTimelineAsync(id, ct));

    [HttpGet("{id:guid}/evidence")]
    public async Task<ActionResult> GetEvidence(Guid id, CancellationToken ct) =>
        Ok(await _incidents.GetEvidenceAsync(id, ct));

    [HttpGet("{id:guid}/actions")]
    public async Task<ActionResult> GetActions(Guid id, [FromServices] IActionService actions, CancellationToken ct) =>
        Ok(await actions.GetActionsForIncidentAsync(id, ct));

    [HttpGet("{id:guid}/report")]
    public async Task<ActionResult> GetReport(Guid id, CancellationToken ct)
    {
        var report = await _incidents.GetReportAsync(id, ct);
        return report is null ? NotFound() : Ok(report);
    }
}

[ApiController]
[Route("api")]
public sealed class DashboardController : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<ActionResult> GetDashboard([FromServices] IIncidentService incidents, CancellationToken ct) =>
        Ok(await incidents.GetDashboardStatsAsync(ct));
}

[ApiController]
[Route("api/actions")]
public sealed class ActionsController : ControllerBase
{
    private readonly IActionService _actions;

    public ActionsController(IActionService actions) => _actions = actions;

    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult> Approve(Guid id, [FromBody] ReviewRequest? request, CancellationToken ct)
    {
        await _actions.ApproveAsync(id, request?.Reviewer ?? "operator", ct);
        return Ok(new { status = "approved" });
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult> Reject(Guid id, [FromBody] ReviewRequest? request, CancellationToken ct)
    {
        await _actions.RejectAsync(id, request?.Reviewer ?? "operator", request?.Notes, ct);
        return Ok(new { status = "rejected" });
    }
}

public sealed record ReviewRequest(string? Reviewer, string? Notes);

[ApiController]
[Route("api/reference-environment")]
public sealed class ReferenceEnvironmentController : ControllerBase
{
    private readonly IReferenceEnvironment _environment;

    public ReferenceEnvironmentController(IReferenceEnvironment environment) => _environment = environment;

    [HttpGet("scenarios")]
    public ActionResult GetScenarios() => Ok(_environment.GetScenarios());

    [HttpPost("scenarios/{id}/reset")]
    public ActionResult ResetScenario(string id)
    {
        _environment.ResetScenario(id);
        return Ok(new { scenarioId = id, status = "reset" });
    }
}
