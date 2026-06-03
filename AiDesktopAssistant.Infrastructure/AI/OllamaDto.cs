using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AiDesktopAssistant.Infrastructure.AI;

internal record OllamaChatRequestDto(
    [property: JsonPropertyName("model")] string Model,
    [property: JsonPropertyName("messages")] List<OllamaMessageDto> Messages,
    [property: JsonPropertyName("stream")] bool Stream,
    [property: JsonPropertyName("options")] Dictionary<string, object>? Options = null
);

internal record OllamaMessageDto(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content
);

internal record OllamaStreamResponseDto(
    [property: JsonPropertyName("message")] OllamaResponseMessageDto? Message,
    [property: JsonPropertyName("done")] bool Done
);

internal record OllamaResponseMessageDto(
    [property: JsonPropertyName("role")] string Role,
    [property: JsonPropertyName("content")] string Content,
    [property: JsonPropertyName("thinking")] string? Thinking
);