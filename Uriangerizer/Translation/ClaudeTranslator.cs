using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace Uriangerizer.Translation;

public sealed class ClaudeTranslator(Configuration config) : ITranslator, IDisposable
{
    private const int MaxTokens = 512;

    private AnthropicClient? client;
    private (string ApiKey, int TimeoutSeconds) clientSettings;

    public async Task<string> TranslateAsync(string text, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            throw new TranslationException("No API key set. Open /uri config.");

        var timeout = TimeSpan.FromSeconds(Math.Max(1, config.TimeoutSeconds));
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        Message response;
        try
        {
            var request = new MessageCreateParams
            {
                Model = config.Model,
                MaxTokens = MaxTokens,
                System = DefaultPrompt.Render(config.GetSystemPrompt(), config.MaxChars),
                Messages = [new() { Role = Role.User, Content = text }],
            };
            // Newer models (e.g. Sonnet 5) think before answering by default, which only adds latency
            // for a one-line translation. Fable/Mythos models reject disabling it, so leave those alone.
            if (!config.Model.StartsWith("claude-fable", StringComparison.OrdinalIgnoreCase) &&
                !config.Model.StartsWith("claude-mythos", StringComparison.OrdinalIgnoreCase))
            {
                request = request with { Thinking = new ThinkingConfigDisabled() };
            }

            response = await GetClient().Messages.Create(request, timeoutCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw; // plugin is unloading; the caller stays silent
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            throw new TranslationException($"Request timed out after {timeout.TotalSeconds:0}s.", ex);
        }
        // Most specific first. Messages are fixed strings so nothing from the request (the key) leaks.
        catch (AnthropicUnauthorizedException ex)
        {
            throw new TranslationException("Invalid API key (401). Check /uri config.", ex);
        }
        catch (AnthropicForbiddenException ex)
        {
            throw new TranslationException("API key lacks permission (403).", ex);
        }
        catch (AnthropicNotFoundException ex)
        {
            throw new TranslationException($"Model '{config.Model}' not found (404). Check /uri config.", ex);
        }
        catch (AnthropicBadRequestException ex)
        {
            throw new TranslationException("Bad request (400). Check the model name and system prompt.", ex);
        }
        catch (AnthropicRateLimitException ex)
        {
            throw new TranslationException("Rate limited (429). Wait a moment and try again.", ex);
        }
        catch (Anthropic5xxException ex)
        {
            throw new TranslationException("Claude API is unavailable or overloaded (5xx). Try again shortly.", ex);
        }
        catch (Exception ex) when (ex is AnthropicIOException or HttpRequestException)
        {
            throw new TranslationException("Network error while contacting the Claude API.", ex);
        }
        catch (AnthropicException ex)
        {
            throw new TranslationException($"Claude API error ({ex.GetType().Name}).", ex);
        }

        if (response.StopReason == "refusal")
            throw new TranslationException("The model refused to translate this message.");

        var output = string.Concat(response.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text));
        if (string.IsNullOrWhiteSpace(output))
            throw new TranslationException("The model returned an empty response.");

        return output;
    }

    // One client (and one underlying HttpClient) is reused across requests;
    // it is rebuilt only when the key or timeout changes in the config window.
    private AnthropicClient GetClient()
    {
        var settings = (config.ApiKey, config.TimeoutSeconds);
        if (client is null || clientSettings != settings)
        {
            client = new AnthropicClient
            {
                ApiKey = settings.ApiKey,
                // We enforce our own timeout above; no retries so the user gets fast feedback.
                Timeout = TimeSpan.FromSeconds(Math.Max(1, settings.TimeoutSeconds) + 5),
                MaxRetries = 0,
            };
            clientSettings = settings;
        }

        return client;
    }

    public void Dispose()
    {
        (client as IDisposable)?.Dispose();
    }
}
