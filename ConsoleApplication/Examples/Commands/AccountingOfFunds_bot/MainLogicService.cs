using ConsoleExample.Models.CommandHeaders;
using ConsoleExample.Services;
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
    [BotHandler]
    internal class MainLogicService
    {
        private IUserSessionManager _sessionManager;
        private FilePathsSettings _settingsInstance;

        // Конструктор формирующий "изоляцию" пользователей др от друга за счёт ключей чата и паттерна DI
        public MainLogicService (IUserSessionManager sessionManager, IOptions<FilePathsSettings> settingsOptions)
        {
            _sessionManager = sessionManager;
            _settingsInstance = settingsOptions.Value; // Извлекаем сам объект настроек
        }

        [InlineCallbackHandler<CustomTHeader>(CustomTHeader.EditBalance)]
        public async Task EditBalance(IBotContext context)
        {
            try
            {
                //Строим меню
                var exampleItemOne = new InlineCallback("Приход средств", CustomTHeader.AddMoney);
                var exampleItemTwo = new InlineCallback("Расходы на материалы", CustomTHeader.CostOfMaterials);
                var exampleItemThree = new InlineCallback("Расходы на оплату труда", CustomTHeader.LaborCosts);
                var exampleItemFour = new InlineCallback("В начало", CustomTHeader.MainMenu);

                //IInlineContent - реализуют все inline кнопки
                List<IInlineContent> menu = new();

                menu.Add(exampleItemOne);
                menu.Add(exampleItemTwo);
                menu.Add(exampleItemThree);
                menu.Add(exampleItemFour);

                //Генерация меню на основе данных в * столбец
                var testMenu = MenuGenerator.InlineKeyboard(1, menu);

                //Создание настроек для передачи в сообщение
                var option = new OptionMessage();
                //Передача меню в настройки
                option.MenuInlineKeyboardMarkup = testMenu;

                // Получаем путь до файла с приветственным фото
                string picturePath = _settingsInstance.GetPicturePath();

                //Отправка сообщение с меню
                _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.SendPhoto(context, context.Update.GetChatId(), "Что именно Вы хотите зафиксировать?", picturePath, option));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка построения меню взаимодействия: {ex.Message}");
            }
        }

        // Помощь
        [InlineCallbackHandler<CustomTHeader>(CustomTHeader.Help)]
        public async Task Help(IBotContext context)
        {
            _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.Send(context, context.Update, "По всем вопросам обращаться: \r\nТг: @The_Warrior_0f_Light"));
        }
    }

}


