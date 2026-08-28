using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiTier2Support.Application.Ai;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AiTier2Support.Infrastructure.Ai;

public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "gemini-3.6-flash";
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta";
}

public sealed class GeminiClient : ILlmClient
{
    private readonly HttpClient _http;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiClient> _logger;

    public GeminiClient(HttpClient http, IOptions<GeminiOptions> options, ILogger<GeminiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<LlmResponse> GenerateAsync(LlmRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
            throw new InvalidOperationException("GEMINI_API_KEY is not configured.");

        var url = $"{_options.BaseUrl}/models/{_options.Model}:generateContent?key={_options.ApiKey}";

        var payload = new
        {
            systemInstruction = new { parts = new[] { new { text = request.SystemPrompt } } },
            contents = request.Messages.Select(MapMessage).ToArray(),
            tools = request.Tools.Count > 0
                ? new[] { new { functionDeclarations = request.Tools.Select(MapTool).ToArray() } }
                : null,
            generationConfig = request.RequireStructuredOutput
                ? new { responseMimeType = "application/json" }
                : null
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(url, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        //if (!response.IsSuccessStatusCode)
        //{
        //    _logger.LogError("Gemini API error: {Status} {Body}", response.StatusCode, body);
        //    throw new InvalidOperationException($"Gemini API request failed: {response.StatusCode}");
        //}
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Gemini API error: {Status} {Body}",
                response.StatusCode,
                body);

            throw new InvalidOperationException(
                $"Gemini API request failed: {response.StatusCode}. Body: {body}");
        }

        var doc = JsonDocument.Parse(body);
        var candidate = doc.RootElement.GetProperty("candidates")[0].GetProperty("content");
        var parts = candidate.GetProperty("parts");

        var toolCalls = new List<LlmToolCall>();
        var textParts = new List<string>();

        foreach (var part in parts.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var textProp))
            {
                var text = textProp.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    textParts.Add(text);
                }
            }

            if (part.TryGetProperty("functionCall", out var fc))
            {
                toolCalls.Add(new LlmToolCall
                {
                    Id =fc.TryGetProperty("id", out var id) ? id.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N"),

                    Name = fc.GetProperty("name")
                        .GetString()
                        ?? string.Empty,

                    ArgumentsJson =
                        fc.TryGetProperty("args", out var args)
                            ? args.GetRawText()
                            : "{}",

                    ThoughtSignature =
                        part.TryGetProperty(
                            "thoughtSignature",
                            out var signature)
                                ? signature.GetString()
                                : null
                });
            }
        }

        return new LlmResponse
        {
            Text = string.Join("\n", textParts),
            ToolCalls = toolCalls,
            StructuredJson = request.RequireStructuredOutput ? string.Join("\n", textParts) : null,
            IsComplete = toolCalls.Count == 0
        };
    }

    private static object MapMessage(LlmMessage msg)
    {
        if (msg.Role == "tool")
        {
            return new
            {
                role = "user",
                parts = new[]
                {
                    new
                    {
                        functionResponse = new
                        {
                            name = msg.ToolName,
                            response = JsonSerializer.Deserialize<object>(msg.Content) ?? new { result = msg.Content }
                        }
                    }
                }
            };
        }

        
        if (msg.ToolCalls is { Count: > 0 })
        {
            return new
            {
                role = "model",

                parts = msg.ToolCalls.Select(tc => new
                {
                    functionCall = new
                    {
                        id = tc.Id,

                        name = tc.Name,

                        args = JsonSerializer.Deserialize<object>(
                            tc.ArgumentsJson)
                    },

                    thoughtSignature = tc.ThoughtSignature
                }).ToArray()
            };
        }
        //
        return new
        {
            role = msg.Role == "assistant" ? "model" : "user",
            parts = new[] { new { text = msg.Content } }
        };
    }

    private static object MapTool(LlmToolDefinition tool) => new
    {
        name = tool.Name,
        description = tool.Description,
        parameters = JsonSerializer.Deserialize<object>(tool.ParametersJsonSchema)
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}
