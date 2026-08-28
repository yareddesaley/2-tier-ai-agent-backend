using AiTier2Support.Application.Actions;
using AiTier2Support.Application.Agents;
using AiTier2Support.Application.Agents.Validators;
using AiTier2Support.Application.Ai;
using AiTier2Support.Application.Common;
using AiTier2Support.Application.Github;
using AiTier2Support.Application.Incidents;
using AiTier2Support.Application.Incidents.Validators;
using AiTier2Support.Application.ReferenceEnvironment;
using AiTier2Support.Application.Tools;
using AiTier2Support.Infrastructure.Agents;
using AiTier2Support.Infrastructure.Ai;
using AiTier2Support.Infrastructure.Github;
using AiTier2Support.Infrastructure.Persistence;
using AiTier2Support.Infrastructure.ReferenceEnvironment;
using AiTier2Support.Infrastructure.Services;
using AiTier2Support.Infrastructure.Tools;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AiTier2Support.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GeminiOptions>(options =>
        {
            options.ApiKey = configuration["GEMINI_API_KEY"] ?? configuration["Gemini:ApiKey"] ?? string.Empty;
            options.Model = configuration["Gemini:Model"] ?? "gemini-2.5-flash";
        });

        services.Configure<GitHubOptions>(options =>
        {
            options.Token = configuration["GITHUB_TOKEN"] ?? configuration["GitHub:Token"] ?? string.Empty;
            options.Owner = configuration["GITHUB_OWNER"] ?? configuration["GitHub:Owner"] ?? string.Empty;
            options.Repository = configuration["GITHUB_REPOSITORY"] ?? configuration["GitHub:Repository"] ?? string.Empty;
        });

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddHttpClient<ILlmClient, GeminiClient>();
        services.AddHttpClient<IGitHubClient, GitHubClient>();

        services.AddSingleton<IReferenceEnvironment, ReferenceEnvironment.ReferenceEnvironment>();
        services.AddSingleton<IActionPolicy, ActionPolicy>();
        services.AddSingleton<IRiskPolicy, ActionPolicy>();

        services.AddSingleton<IAgentTool, CheckServiceHealthTool>();
        services.AddSingleton<IAgentTool, GetServiceMetricsTool>();
        services.AddSingleton<IAgentTool, SearchApplicationLogsTool>();
        services.AddSingleton<IAgentTool, GetDatabaseMetricsTool>();
        services.AddSingleton<IAgentTool, GetRecentDeploymentTool>();
        services.AddSingleton<IAgentTool, GetDeploymentDetailsTool>();
        services.AddSingleton<IAgentTool, RestartWorkerTool>();
        services.AddSingleton<IAgentTool, RollbackDeploymentTool>();
        services.AddSingleton<IAgentTool, VerifyServiceHealthTool>();
        services.AddSingleton<IAgentTool, SubmitDiagnosisTool>();
        services.AddSingleton<IAgentToolRegistry, AgentToolRegistry>();

        services.AddScoped<IAgentOrchestrator, AgentOrchestrator>();
        services.AddScoped<IIncidentService, IncidentService>();
        services.AddScoped<IInvestigationService, InvestigationService>();
        services.AddScoped<IActionService, ActionService>();

        services.AddScoped<IValidator<CreateIncidentRequest>, CreateIncidentRequestValidator>();
        services.AddScoped<IValidator<AgentDiagnosis>, AgentDiagnosisValidator>();

        return services;
    }
}
