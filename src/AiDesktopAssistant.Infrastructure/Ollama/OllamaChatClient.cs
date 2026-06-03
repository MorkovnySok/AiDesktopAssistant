using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiDesktopAssistant.Core.Ai;
using AiDesktopAssistant.Core.Entities;
using AiDesktopAssistant.Core.Interfaces;

namespace AiDesktopAssistant.Infrastructure.Ollama;

public sealed class OllamaChatClient(HttpClient httpClient) : IAiChatClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async IAsyncEnumerable<AiStreamEvent> StreamAsync(
        AiChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var ollamaRequest = new OllamaChatRequest(
            request.Model,
            BuildMessages(request),
            Stream: true,
            Think: request.Think);

        var startResult = await SendRequestAsync(ollamaRequest, cancellationToken);
        if (startResult.ErrorText is not null)
        {
            yield return new AiStreamEvent(AiStreamEventType.Content, startResult.ErrorText);
            yield return new AiStreamEvent(AiStreamEventType.Completed);
            yield break;
        }

        var response = startResult.Response;
        if (response is null)
        {
            yield return new AiStreamEvent(AiStreamEventType.Content, "Ollama request failed before a response was returned.");
            yield return new AiStreamEvent(AiStreamEventType.Completed);
            yield break;
        }

        try
        {
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                yield return new AiStreamEvent(AiStreamEventType.Content, FormatHttpError(response, errorBody));
                yield return new AiStreamEvent(AiStreamEventType.Completed);
                yield break;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream, Encoding.UTF8);

            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null)
                {
                    break;
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                OllamaChatResponse? chunk;
                chunk = JsonSerializer.Deserialize<OllamaChatResponse>(line, JsonOptions);

                if (!string.IsNullOrEmpty(chunk?.Message?.Thinking))
                {
                    yield return new AiStreamEvent(AiStreamEventType.Reasoning, chunk.Message.Thinking);
                }

                if (!string.IsNullOrEmpty(chunk?.Message?.Content))
                {
                    yield return new AiStreamEvent(AiStreamEventType.Content, chunk.Message.Content);
                }

                if (chunk?.Done == true)
                {
                    yield return new AiStreamEvent(AiStreamEventType.Completed);
                    yield break;
                }
            }

            if (cancellationToken.IsCancellationRequested)
            {
                yield return new AiStreamEvent(AiStreamEventType.Content, "\n[Request cancelled.]\n");
            }

            yield return new AiStreamEvent(AiStreamEventType.Completed);
        }
        finally
        {
            response?.Dispose();
        }
    }

    private async Task<(HttpResponseMessage? Response, string? ErrorText)> SendRequestAsync(
        OllamaChatRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
            {
                Content = new StringContent(JsonSerializer.Serialize(request, JsonOptions), Encoding.UTF8, "application/json")
            };

            var response = await httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return (response, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return (null, "\n[Request cancelled.]\n");
        }
        catch (HttpRequestException ex)
        {
            return (null,
                $"Unable to reach Ollama at '{httpClient.BaseAddress}'. Make sure Ollama is running and the configured model is available. {ex.Message}");
        }
        catch (TaskCanceledException ex)
        {
            return (null, $"Ollama request timed out or was cancelled. {ex.Message}");
        }
    }

    private static IReadOnlyList<OllamaChatMessage> BuildMessages(AiChatRequest request)
    {
        var messages = new List<OllamaChatMessage>();

        if (!string.IsNullOrWhiteSpace(request.SystemPrompt))
        {
            messages.Add(new OllamaChatMessage("system", request.SystemPrompt));
        }

        messages.AddRange(request.Messages.Select(message => new OllamaChatMessage(ToOllamaRole(message.Role), message.Content)));
        return messages;
    }

    private static string ToOllamaRole(MessageRole role) => role switch
    {
        MessageRole.System => "system",
        MessageRole.User => "user",
        MessageRole.Assistant => "assistant",
        _ => "user"
    };

    private static string FormatHttpError(HttpResponseMessage response, string errorBody)
    {
        var body = string.IsNullOrWhiteSpace(errorBody) ? "No error body was returned." : errorBody.Trim();
        return $"Ollama request failed with HTTP {(int)response.StatusCode} ({response.ReasonPhrase}). {body}";
    }

    private sealed record OllamaChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")]
        IReadOnlyList<OllamaChatMessage> Messages,
        [property: JsonPropertyName("stream")] bool Stream,
        [property: JsonPropertyName("think")] bool Think);

    private sealed record OllamaChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")]
        string Content);

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")]
        OllamaMessage? Message,
        [property: JsonPropertyName("done")] bool Done);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("role")] string? Role,
        [property: JsonPropertyName("content")]
        string? Content,
        [property: JsonPropertyName("thinking")]
        string? Thinking);
}
