using ConsoleExample.Attributes;
using ConsoleExample.Models.CommandHeaders;
using Microsoft.Extensions.Options;
using OfficeOpenXml;
using PRTelegramBot.Attributes;
using PRTelegramBot.Extensions;
using PRTelegramBot.Interfaces;
using PRTelegramBot.Models;
using PRTelegramBot.Models.InlineButtons;
using PRTelegramBot.Utils;
using Telegram.Bot;
using Helpers = PRTelegramBot.Helpers;

namespace ConsoleExample.Examples.Commands.AccountingOfFunds_bot
{
    [BotHandler]
    internal class BalanceService
    {
        private IUserSessionManager _sessionManager;
        private FilePathsSettings _settingsInstance;

        // Определяем конструктор этого класса для создания "изоляции" пользователей друг от друга
        public BalanceService(IUserSessionManager sessionManager, IOptions<FilePathsSettings> settingsOptions)
        {
            _sessionManager = sessionManager;
            _settingsInstance = settingsOptions.Value; // Извлекаем сам объект настроек
        }

        // Первочерёдно, получаем контроль над нужным эксель файлом, чтобы на его основе получить баланс и отправить его же.
        [InlineCallbackHandler<CustomTHeader>(CustomTHeader.ChechkingBalance)]
        public async Task ExcleHandler(IBotContext context)
        {
            try
            {
                // Устанавливаем бесплатную лицензию для некоммерческого использования
                ExcelPackage.License.SetNonCommercialPersonal("Vladimir");

                // Получаем путь к Excel файлу с данными
                string exclePath = _settingsInstance.GetExcelPath();

                // Открываем/создаем Excel файл
                FileInfo fileInfo = new FileInfo(exclePath);

                // Проверяем существование файла
                if (!fileInfo.Exists)
                {
                    await Helpers.Message.Send(context, context.Update, "Проверьте наличие отчёта, как файла ...");
                    return; // Отменяем дальнейшее выполнение логики
                }

                using (var package = new ExcelPackage(fileInfo))
                {
                    // Включаем вычисление формул
                    package.Workbook.Calculate();

                    var worksheet = package.Workbook.Worksheets[0];

                    if (worksheet == null)
                    {
                        await Helpers.Message.Send(context, context.Update, "Ошибка: лист не найден");
                        return;
                    }

                    // Получаем ячейку по конкретному адресу
                    var cell = worksheet.Cells["S3"];

                    //// Вычисляем формулу вручную (заставляем ячейку выполнить формулу внутри неё)
                    //cell.Calculate();

                    // Получаем отформатированное значение
                    var cellValue = cell.Text; // Для отображения как в Excel
                                               // Или для получения числового значения:
                                               // var cellValue = cell.GetValue<decimal>().ToString();

                    if (string.IsNullOrEmpty(cellValue))
                    {
                        await Helpers.Message.Send(context, context.Update, "Ошибка: ячейка S3 пуста");
                        return;
                    }

                    // Строим меню
                    var exampleItemOne = new InlineCallback("Получить файл excel", CustomTHeader.TakeFile);
                    var exampleItemTwo = new InlineCallback("Вернуться в меню", CustomTHeader.MainMenu);

                    // IInlineContent - реализуют все inline кнопки
                    List<IInlineContent> menu = new();

                    menu.Add(exampleItemOne);
                    menu.Add(exampleItemTwo);

                    // Генерация меню на основе данных в * столбец
                    var testMenu = MenuGenerator.InlineKeyboard(1, menu);

                    // Создание настроек для передачи в сообщение
                    var option = new OptionMessage();
                    // Передача меню в настройки
                    option.MenuInlineKeyboardMarkup = testMenu;

                    ///Неккоректно получает обработчик, т.к. update приходит не из текста, а от нажатия инлайн-кнопки
                    //var handler = update.GetStepHandler<StepTelegram>(); // Получаем текущий обработчик
                    //handler!.GetCache<StepCache>().BalanceService = cellValue.ToString();

                    _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.Send(context, context.Update, $"Текущий баланс: {cellValue}", option));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка построения меню: {ex.Message}");
            }
        }

        // Отправляет Excel документ пользователю
        [AdminOnlyExample]
        [InlineCallbackHandler<CustomTHeader>(CustomTHeader.TakeFile)]
        public async Task TakeFile(IBotContext context)
        {

            // Удаляем сообщение пользователя
            await context.BotClient.DeleteMessage(context.Update.GetChatId(), context.Update.GetMessageId());

            //Строим меню
            var exampleItemOne = new InlineCallback("В начало", CustomTHeader.MainMenu);

            //IInlineContent - реализуют все inline кнопки
            List<IInlineContent> menu = new();

            menu.Add(exampleItemOne);

            //Генерация меню на основе данных в * столбец
            var testMenu = MenuGenerator.InlineKeyboard(1, menu);

            //Создание настроек для передачи в сообщение
            var option = new OptionMessage();
            //Передача меню в настройки
            option.MenuInlineKeyboardMarkup = testMenu;

            // Получаем путь к Excel файлу с данными
            string exclePath = _settingsInstance.GetExcelPath();

            // Открываем/создаем Excel файл
            FileInfo fileInfo = new FileInfo(exclePath);

            // Проверяем существование файла
            if (!fileInfo.Exists)
            {
                _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.Send(context, context.Update, "Проверьте наличие отчёта, как файла ..."));
                return; // Отменяем дальнейшее выполнение логики
            }

            // Отправляем файл пользователю
            using (var fileStream = new FileStream(exclePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var sendMessage = await Helpers.Message.SendFile(
                    context: context,
                    chatId: context.Update.GetChatId(),
                    text: "",
                    filePath: exclePath,
                    option: option);

                // Добавляем отправленное сообщение в список для удаления
                _sessionManager.AddMessageForDelete(context.Update.GetChatId(), sendMessage);
            }
        }
    }
}
