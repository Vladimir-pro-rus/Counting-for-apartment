using PRTelegramBot.Attributes;


namespace ConsoleExample.Services
{
    /// <summary>
    /// Сервис для получения и обновления OAuth-токенов Яндекса
    /// </summary>
    public interface IYandexTokenService // Запускается таким образом:
                                         // Вызывается метод (класс) сохранения фото -> Конструктор класса сохранения фото ждёт "IYandexTokenService".
    {
        Task<string> GetAccessTokenAsync(); // Получить токен
    }

    [BotHandler]
    public class YandexTokenService : IYandexTokenService
    {
        private string _accessToken;

        // Инициализирует данные в конструкторе.
        // Куда они попадают из конфигурации (инициализация в DI).
        public YandexTokenService(string accessToken)
        {
            _accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken)); // ?? - читается как: “если левое значение не null — возьми его, иначе — правое”
                                                                                                // throw - Говорит: “выброси исключение прямо сейчас”. Прерывает выполнение и передаёт ошибку выше
                                                                                                // ArgumentNullException - Специальный класс в .NET, который означает: “передан null туда, где он не разрешён”.
                                                                                                // nameof - автоматическое обновление имени параметра, если его имя было изменено
        }

        public Task<string> GetAccessTokenAsync() => Task.FromResult(_accessToken);
    }
