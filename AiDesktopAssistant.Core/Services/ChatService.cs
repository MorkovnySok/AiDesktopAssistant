using System.Runtime.CompilerServices;
using AiDesktopAssistant.Core.Domain;
using AiDesktopAssistant.Core.Interfaces;
using AiDesktopAssistant.Core.Models;

namespace AiDesktopAssistant.Core.Services
{
    public class ChatService(IConversationRepository repository, IAiChatClient aiClient, string defaultModel)
        : IChatService
    {
        // Берется из настроек, например

        public async Task<Conversation> CreateConversationAsync(CancellationToken cancellationToken = default)
        {
            var conversation = await repository.CreateAsync(cancellationToken);
            return conversation;
        }

        // Ключевое слово async здесь ОБЯЗАТЕЛЬНО, если внутри используется yield return.
        // Атрибут [EnumeratorCancellation] гарантирует, что токен отмены прокинется в итератор.
        public async IAsyncEnumerable<AiStreamEvent> SendMessageAsync(
            Guid conversationId,
            string userMessage,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            // 1. Загружаем историю
            var conversation = await repository.GetByIdAsync(conversationId, cancellationToken);
            if (conversation == null)
            {
                throw new ArgumentException($"Разговор с ID {conversationId} не найден.");
            }

            // 2. Добавляем сообщение пользователя
            var userChatMsg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = MessageRole.User,
                Content = userMessage,
                CreatedAt = DateTime.UtcNow
            };
            conversation.Messages.Add(userChatMsg);

            // 3. Создаем пустое сообщение ассистента, куда будем накапливать стрим
            var assistantChatMsg = new ChatMessage
            {
                Id = Guid.NewGuid(),
                ConversationId = conversationId,
                Role = MessageRole.Assistant,
                Content = string.Empty,
                ReasoningContent = string.Empty,
                CreatedAt = DateTime.UtcNow
            };
            conversation.Messages.Add(assistantChatMsg);

            // 4. Формируем запрос для AI (маппим внутреннюю историю в DTO для запроса)
            var mappedMessages = new List<AiChatMessage>();
            foreach (var msg in conversation.Messages)
            {
                // Не отправляем в Ollama последнее, еще пустое сообщение ассистента
                if (msg.Id == assistantChatMsg.Id)
                {
                    continue;
                }

                mappedMessages.Add(new AiChatMessage(msg.Role, msg.Content));
            }

            var aiRequest = new AiChatRequest(defaultModel, mappedMessages, true, null);

            // 5. Потребляем стрим от IAiChatClient и перенаправляем его в UI через yield return
            await foreach (var streamEvent in aiClient.StreamAsync(aiRequest, cancellationToken))
            {
                if (streamEvent.Type == AiStreamEventType.Reasoning)
                {
                    assistantChatMsg.ReasoningContent += streamEvent.Text;
                }
                else if (streamEvent.Type == AiStreamEventType.Content)
                {
                    assistantChatMsg.Content += streamEvent.Text;
                }

                // Передаем чанк дальше в UI слой
                yield return streamEvent;
            }

            // 6. Стрим завершен, обновляем метаданные и сохраняем состояние
            conversation.UpdatedAt = DateTime.UtcNow;
            await repository.SaveAsync(conversation, cancellationToken);
        }
    }
}