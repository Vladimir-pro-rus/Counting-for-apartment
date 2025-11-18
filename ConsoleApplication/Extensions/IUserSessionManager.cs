using PRTelegramBot.Interfaces;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace ConsoleExample.Examples.Commands.AccountingOfFunds_bot
{
    // Интерфейс для решения проблемы с глобальным доступом
    public interface IUserSessionManager // Набор методов без реализации, но с запр. переменными. Его наследователь обязан реализовать их все
    {
        void AddMessageForDelete(long chatId, Message message);
        //List<Message> GetMessagesForDelete(long chatId);
        Task ClearMessagesForDelete(long chatId, IBotContext context);
    }

    // Контейнер, реализующий интерфейс "IUserSessionManager"
    public class UserSessionManagerImpl : IUserSessionManager
    {
        private readonly Dictionary<long, List<Message>> _messagesPerUser = new(); // Словарь, учитывающий сообщения для каждого пользователя

        // Добавляет сообщение для удаления для каждого пользователя отдельно
        public void AddMessageForDelete(long chatId, Message message)
        {
            if (!_messagesPerUser.ContainsKey(chatId)) // 
            {
                _messagesPerUser[chatId] = new List<Message>();
            }
            _messagesPerUser[chatId].Add(message);
        }

        // Удаляет сообщения для конкретного пользователя из чата Telegram и из внутреннего списка
        public async Task ClearMessagesForDelete(long chatId, IBotContext context)
        {
            // Проверяем, есть ли список сообщений для этого chatId и присваиваем его переменной
            if (_messagesPerUser.TryGetValue(chatId, out var messagesToDelete))
            {
                // Проверяем, не пустой ли список
                if (messagesToDelete.Count > 0)
                {
                    // Перебираем список и удаляем каждое сообщение через BotClient
                    foreach (var message in messagesToDelete.ToList()) // ToList() создаёт копию, на случай если список изменится во время итерации
                    {
                        try
                        {
                            await context.BotClient.DeleteMessage(message.Chat.Id, message.MessageId);
                        }
                        catch (Exception ex)
                        {
                            // Логируем ошибку, если сообщение не удалось удалить
                            Console.WriteLine($"Ошибка при удалении сообщения {message.MessageId} из чата {message.Chat.Id}: {ex.Message}");
                        }
                    }
                    messagesToDelete.Clear(); // После удаления всех сообщений из чата, очищаем внутренний список
                }
                else
                {
                    // Список существует, но пустой
                    Console.WriteLine($"Диалог с пользователем (ID: {chatId}) -> пуст. Нечего удалять.");
                }
            }
            else
            {
                // Списка не существует для этого chatId
                Console.WriteLine($"Диалог с пользователем (ID: {chatId}) -> пуст. Список не инициализирован.");
            }
        }
    }
}
