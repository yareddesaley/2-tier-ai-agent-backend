using System.Net.Http.Headers;
using System.Text.Json;
using AiTier2Support.Application.Github;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiTier2Support.Infrastructure.Github;

public sealed class GitHubOptions
{
    public const string SectionName = "GitHub";
    public string Token { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
}

public sealed class GitHubClient : IGitHubClient
{
    private readonly HttpClient _http;
    private readonly GitHubOptions _options;
    private readonly ILogger<GitHubClient> _logger;

    public GitHubClient(HttpClient http, IOptions<GitHubOptions> options, ILogger<GitHubClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (IsConfigured)
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.Token);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("AiTier2Support/1.0");
            _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        }
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.Token) &&
        !string.IsNullOrWhiteSpace(_options.Owner) &&
        !string.IsNullOrWhiteSpace(_options.Repository);

    public async Task<IReadOnlyList<GitHubDeployment>> GetRecentDeploymentsAsync(int count, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return [];

        try
        {
            var url = $"https://api.github.com/repos/{_options.Owner}/{_options.Repository}/deployments?per_page={count}";
            var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return [];

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.EnumerateArray().Select(MapDeployment).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub deployments fetch failed");
            return [];
        }
    }

    public async Task<GitHubDeployment?> GetDeploymentAsync(string deploymentId, CancellationToken cancellationToken)
    {
        if (!IsConfigured) return null;

        try
        {
            var url = $"https://api.github.com/repos/{_options.Owner}/{_options.Repository}/deployments/{deploymentId}";
            var response = await _http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            return MapDeployment(doc.RootElement);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub deployment fetch failed for {Id}", deploymentId);
            return null;
        }
    }

    private static GitHubDeployment MapDeployment(JsonElement el) => new()
    {
        Id = el.GetProperty("id").GetInt64().ToString(),
        Sha = el.GetProperty("sha").GetString() ?? string.Empty,
        Ref = el.GetProperty("ref").GetString() ?? string.Empty,
        Environment = el.GetProperty("environment").GetString() ?? "production",
        CreatedAt = el.GetProperty("created_at").GetDateTime(),
        Creator = el.TryGetProperty("creator", out var c) && c.TryGetProperty("login", out var login)
            ? login.GetString() : null,
        Description = el.TryGetProperty("description", out var d) ? d.GetString() : null
    };
}
