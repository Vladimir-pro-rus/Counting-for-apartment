using ConsoleExample.Examples.Commands.AccountingOfFunds_bot;
using ConsoleExample.Extension;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PRTelegramBot.Core;
using PRTelegramBot.Extensions;
using PRTelegramBot.Models.EventsArgs;
using Telegram.Bot;
using Helpers = PRTelegramBot.Helpers;

namespace ConsoleExample.Examples.Events
{
    public static class ExampleEvents
    {
        public static async Task OnWrongTypeChat(BotEventArgs e)
        {
            string msg = "Неверный тип чата";
            await Helpers.Message.Send(e.Context, msg);
        }

        // Вызывает логику каждый раз, когда команда от пользователя не была опознана
        public static async Task OnMissingCommand(PRTelegramBot.Models.EventsArgs.BotEventArgs args)
        {
            var userMessage = args.Context;

            if (userMessage != null && userMessage.Update?.Message != null)
            {
                // Получаем сервисы из ядра бота
                var servisProvider = args.Context.Current.Options.ServiceProvider.GetService<IUserSessionManager>(); 
                var options = args.Context.Current.Options.ServiceProvider.GetService<IOptions<FilePathsSettings>>();
                
                // Вручную создаём экземпляр нужного класса и передаём в его конструктор нужные объекты
                NavigationService navigationService = new NavigationService(servisProvider, options);

                await args.Context.BotClient.DeleteMessage(args.Context.GetChatId(), userMessage.GetMessageId()); // Удаляем сообщение пользователя

                if (navigationService != null && servisProvider != null && options != null)
                await navigationService.NavigateToMainMenu(args.Context); // Вызываем меню, если получилось создать экземпляр
            }
            else
                Console.WriteLine("Ошибка при добавлении сообщения в список на удаление");
        }


        public static async Task OnErrorCommand(BotEventArgs args)
        {
            string msg = "Произошла ошибка при обработке команды";
            await Helpers.Message.Send(args.Context, msg);
        }

        /// <summary>
        /// Событие проверки привилегий пользователя
        /// </summary>
        /// <param name="callback">callback функция выполняется в случае успеха</param>
        /// <param name="mask">Маска доступа</param>
        /// Подписка на событие проверки привелегий <see cref="Program"/>
        public static async Task OnCheckPrivilege(PrivilegeEventArgs e)
        {
            if (!e.Mask.HasValue)
            {
                // Нет маски доступа, выполняем метод.
                await e.ExecuteMethod(e.Context);
                return;
            }

            // Получаем значение маски требуемого доступа.
            var requiredAccess = e.Mask.Value;

            // Получаем флаги доступа пользователя.
            // Здесь вы на свое усмотрение реализываете логику получение флагов, например можно из базы данных получить.
            var userFlags = e.Context.Update.LoadExampleFlagPrivilege();

            if (requiredAccess.HasFlag(userFlags))
            {
                // Доступ есть, выполняем метод.
                await e.ExecuteMethod(e.Context);
                return;
            }

            // Доступа нет.
            string errorMsg = "У вас нет доступа к данной функции";
            await Helpers.Message.Send(e.Context, errorMsg);
            return;

        }

        public static async Task OnUserStartWithArgs(StartEventArgs args)
        {
            string msg = "Пользователь отправил старт с аргументом";
            await Helpers.Message.Send(args.Context, msg);
        }
        public static async Task OnWrongTypeMessage(BotEventArgs e)
        {
            string msg = "Неверный тип сообщения";
            await Helpers.Message.Send(e.Context, msg);
        }
    }
}
