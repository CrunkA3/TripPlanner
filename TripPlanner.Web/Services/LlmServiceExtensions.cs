using Microsoft.Extensions.Http.Resilience;
using TripPlanner.Web.Services.OpenAI;

namespace TripPlanner.Web.Services;

internal static class LlmServiceExtensions
{
    internal static IHostApplicationBuilder AddLlmServices(this IHostApplicationBuilder builder)
    {
        // Register a separate HttpClient for user-supplied URL fetches that must not follow
        // redirects automatically; each redirect Location is validated against UrlSecurityHelper
        // before being followed, preventing redirect-based SSRF attacks.
        builder.Services.AddHttpClient("UrlFetchNoRedirect", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 TripPlanner/1.0");
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false
        });

        // Register HttpClient for Ollama (local LLM)
        // Prefer the Aspire-injected connection string ("ollama"), fall back to explicit config or localhost
        var ollamaBaseUrl = builder.Configuration.GetConnectionString("ollama")
            ?? builder.Configuration["OLLAMA_LLAMA3_2_URI"]
            ?? builder.Configuration["Ollama:BaseUrl"]
            ?? "http://localhost:11434";
        // Remove the default 30-second Polly pipeline added by Aspire's service defaults,
        // then add a replacement pipeline with timeouts appropriate for slow LLM inference.
#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is experimental
        builder.Services.AddHttpClient("Ollama", client =>
        {
            client.BaseAddress = new Uri(ollamaBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(3);
        })
        .RemoveAllResilienceHandlers()
        .AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
            options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(3);
            // SamplingDuration must be at least twice the AttemptTimeout to pass validation.
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(10);
        });

        // Register HttpClient for OpenAI (cloud LLM – used when AI:Provider = "OpenAI")
        // OpenAI:BaseUrl can be overridden to point at any OpenAI-compatible proxy (e.g. LiteLLM, LocalAI, Ollama's OpenAI shim).
        // Note: Azure OpenAI uses a different URL path and auth header and is not supported out-of-the-box.
        var openAIBaseUrl = builder.Configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com";
        var openAIApiKey = builder.Configuration["OpenAI:ApiKey"] ?? string.Empty;
        builder.Services.AddHttpClient("OpenAI", client =>
        {
            client.BaseAddress = new Uri(openAIBaseUrl);
            client.Timeout = TimeSpan.FromMinutes(3);
            if (!string.IsNullOrWhiteSpace(openAIApiKey))
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", openAIApiKey);
        })
        .RemoveAllResilienceHandlers()
        .AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(3);
            options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(3);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(10);
        });
#pragma warning restore EXTEXP0001

        // Register AI services based on the configured provider
        var aiProviderRaw = builder.Configuration["AI:Provider"];
        var aiProvider = string.IsNullOrWhiteSpace(aiProviderRaw)
            ? "OpenAI"
            : aiProviderRaw.Trim();

        if (string.Equals(aiProvider, "OpenAI", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddScoped<IPlaceAnalysisService, OpenAIPlaceAnalysisService>();
            builder.Services.AddScoped<IChatService, OpenAIChatService>();
            builder.Services.AddScoped<ISemanticSearchService, OpenAISemanticSearchService>();
        }
        else if (string.Equals(aiProvider, "Ollama", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddScoped<IPlaceAnalysisService, OllamaPlaceAnalysisService>();
            builder.Services.AddScoped<IChatService, OllamaChatService>();
            builder.Services.AddScoped<ISemanticSearchService, OllamaSemanticSearchService>();
        }
        else
        {
            throw new InvalidOperationException(
                $"Invalid AI provider '{aiProviderRaw}'. Valid values are 'OpenAI' or 'Ollama'.");
        }

        return builder;
    }
}
