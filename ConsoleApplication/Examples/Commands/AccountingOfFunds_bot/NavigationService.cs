// INavigationService.cs
using ConsoleExample.Models.CommandHeaders;
using Microsoft.Extensions.Options;
using PRTelegramBot.Attributes;
using PRTelegramBot.Extensions;
using PRTelegramBot.Interfaces;
using PRTelegramBot.Models;
using PRTelegramBot.Models.InlineButtons;
using PRTelegramBot.Utils;
using Helpers = PRTelegramBot.Helpers;

namespace ConsoleExample.Examples.Commands.AccountingOfFunds_bot
{
    public interface INavigationService
    {
        Task NavigateToMainMenu(IBotContext context);
    }

    [BotHandler]
    internal class NavigationService : INavigationService
    {
        private readonly IUserSessionManager _sessionManager; // Обект для фиксации сообщений к удалению
        private readonly FilePathsSettings _settings; // Пути используемых файлов

        // Конструктор класса с инициализацией нужных зависимостей
        public NavigationService(IUserSessionManager sessionManager, IOptions<FilePathsSettings> settingsOptions)
        {
            _sessionManager = sessionManager;
            _settings = settingsOptions.Value;
        }

        [InlineCallbackHandler<CustomTHeader>(CustomTHeader.MainMenu)]
        public async Task NavigateToMainMenu(IBotContext context)
        {
            await _sessionManager.ClearMessagesForDelete(context.GetChatId(), context); // Чистим чат

            // Строим меню
            var exampleItemOne = new InlineCallback("Проверить баланс", CustomTHeader.ChechkingBalance);
            var exampleItemTwo = new InlineCallback("Редактировать данные", CustomTHeader.EditBalance);
            var exampleItemThree = new InlineCallback("Помощь", CustomTHeader.Help);

            List<IInlineContent> menu = new();
            menu.Add(exampleItemOne);
            menu.Add(exampleItemTwo);
            menu.Add(exampleItemThree);

            var testMenu = MenuGenerator.InlineKeyboard(2, menu);

            var option = new OptionMessage();
            option.MenuInlineKeyboardMarkup = testMenu;

            string picturePath = _settings.GetPicturePath();

            // Используем _sessionManager, который доступен внутри экземпляра этого класса
            var sentMessage = await Helpers.Message.SendPhoto(context, context.Update.GetChatId(), "", picturePath, option);
            _sessionManager.AddMessageForDelete(context.Update.GetChatId(), sentMessage);
        }


    }
}
