using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AiDesktopAssistant.Core.Domain;
using AiDesktopAssistant.Core.Interfaces;
using AiDesktopAssistant.Core.Models;

namespace AiDesktopAssistant.Infrastructure
{
    public class OllamaChatClient : IAiChatClient
    {
        private readonly HttpClient _httpClient;

        public OllamaChatClient(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async IAsyncEnumerable<AiStreamEvent> StreamAsync(
            AiChatRequest request, 
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // Переменная для перехвата ошибок из блока try, 
            // чтобы обработать их и сделать yield return ВНЕ catch
            Exception? exceptionToHandle = null;

            // 1. Формируем запрос к Ollama API (внутренние DTO)
            var ollamaRequest = new OllamaChatRequestDto
            {
                Model = request.Model,
                Messages = MapMessages(request.Messages),
                Stream = true,
                Options = new Dictionary<string, object> { { "num_predict", -1 } } // Пример опций
            };

            // Если модель поддерживает разделение мышления (например, DeepSeek R1)
            // Примечание: Ollama включает think по умолчанию, если модель это умеет.

            HttpResponseMessage? response = null;

            try
            {
                response = await _httpClient.PostAsJsonAsync("/api/chat", ollamaRequest, cancellationToken);
                response.EnsureSuccessStatusCode();

                using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var reader = new StreamReader(stream);

                // Заменяем reader.EndOfStream на чтение в цикле до null (асинхронно)
                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    var chunk = JsonSerializer.Deserialize<OllamaChatResponseDto>(line, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (chunk?.Message != null)
                    {
                        // Обработка тегов <think> или полей, если Ollama разделяет контент
                        // (В зависимости от версии Ollama, reasoning может идти в поле 'thinking' или внутри тегов)
                        
                        if (!string.IsNullOrEmpty(chunk.Message.Thinking))
                        {
                            yield return new AiStreamEvent 
                            { 
                                Type = AiStreamEventType.Reasoning, 
                                Text = chunk.Message.Thinking 
                            };
                        }
                        
                        if (!string.IsNullOrEmpty(chunk.Message.Content))
                        {
                            yield return new AiStreamEvent 
                            { 
                                Type = AiStreamEventType.Content, 
                                Text = chunk.Message.Content 
                            };
                        }
                    }

                    if (chunk?.Done == true)
                    {
                        yield return new AiStreamEvent { Type = AiStreamEventType.Completed };
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Обычная отмена пользователем, гасить ошибку или логировать
                exceptionToHandle = new Exception("Запрос был отменен пользователем.");
            }
            catch (Exception ex)
            {
                // Сохраняем ошибку, чтобы выбросить событие снаружи catch
                exceptionToHandle = ex;
            }
            finally
            {
                response?.Dispose();
            }

            // 2. Обработка ошибок в безопасной зоне (ВНЕ блока catch)
            if (exceptionToHandle != null)
            {
                yield return new AiStreamEvent
                {
                    Type = AiStreamEventType.Content,
                    Text = $"\n[Ошибка Ollama]: {exceptionToHandle.Message}"
                };
                yield return new AiStreamEvent { Type = AiStreamEventType.Completed };
            }
        }

        private List<OllamaMessageDto> MapMessages(IReadOnlyList<AiChatMessage> messages)
        {
            var result = new List<OllamaMessageDto>();
            foreach (var m in messages)
            {
                result.Add(new OllamaMessageDto
                {
                    Role = m.Role.ToString().ToLowerInvariant(), // "user", "assistant", "system"
                    Content = m.Content
                });
            }
            return result;
        }
    }

    // Внутренние DTO, которые не светятся в Core и App слоях
    internal class OllamaChatRequestDto
    {
        public string Model { get; set; } = string.Empty;
        public List<OllamaMessageDto> Messages { get; set; } = new();
        public bool Stream { get; set; }
        public Dictionary<string, object>? Options { get; set; }
    }

    internal class OllamaMessageDto
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    internal class OllamaChatResponseDto
    {
        public string Model { get; set; } = string.Empty;
        public OllamaMessageResponseDto? Message { get; set; }
        public bool Done { get; set; }
    }

    internal class OllamaMessageResponseDto
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string? Thinking { get; set; } // Для моделей с явным полем мышления
    }
}
