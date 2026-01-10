using ConsoleExample.Attributes;
using ConsoleExample.Models;
using ConsoleExample.Models.CommandHeaders;
using Microsoft.Extensions.Options;
using OfficeOpenXml;
using PRTelegramBot.Attributes;
using PRTelegramBot.Extensions;
using PRTelegramBot.Interfaces;
using PRTelegramBot.Models;
using PRTelegramBot.Models.InlineButtons;
using PRTelegramBot.Utils;
using System.Globalization;
using System.Text.RegularExpressions;
using Helpers = PRTelegramBot.Helpers;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration.Json;
using ConsoleExample.Services;
using Telegram.Bot;


// Работает с файлом excel.
// Необходимо разделить логику и соблюсти SOLID
namespace ConsoleExample.Examples.Commands.AccountingOfFunds_bot
{
    [BotHandler]
    internal class WorkingWithMoneyService
    {
        private IUserSessionManager _sessionManager;
        private FilePathsSettings _settingsInstance;
        private INavigationService _navigationService;
        private readonly IYandexTokenService _yandexTokenService;

        // Определяем конструктор этого класса для создания "изоляции" пользователей друг от друга
        public WorkingWithMoneyService(
            IUserSessionManager sessionManager,
            IOptions<FilePathsSettings> settingsOptions,
            INavigationService navigationService,
            IYandexTokenService yandexTokenService)
        {
            _sessionManager = sessionManager; // Разделение пользователей
            _settingsInstance = settingsOptions.Value; // Обращаемся к самому объекту настроек
            _navigationService = navigationService; // Главное меню
            _yandexTokenService = yandexTokenService; // Для работы с Яндекс.Диск
        }

        #region CustomTHeader.AddMoney

        // Начинает пошаговую работу
        [AdminOnlyExample]
        [InlineCallbackHandler<CustomTHeader>(CustomTHeader.AddMoney)]
        public async Task AddMoney(IBotContext context)
        {
            context.Update.ClearCacheData();

            string msg = "Пришлите дату зачисления средств, в формате: ДД.ММ.ГГГГ" +
                "\nИли выберите сегодняшнюю 👇";

            string todayDate = DateTime.Now.ToString("dd.MM.yyyy"); // .ToString("dd.MM.yyyy") - преобразует дату в строку, используя указанный формат.

            // Строим меню
            var exampleItemOne = new InlineCallback($"{todayDate}", CustomTHeader.AddDateForAddMoney);

            // IInlineContent - реализуют все inline кнопки
            List<IInlineContent> menu = new();

            menu.Add(exampleItemOne);

            // Генерация меню на основе данных в * столбец
            var testMenu = MenuGenerator.InlineKeyboard(1, menu);

            // Создание настроек для передачи в сообщение
            var option = new OptionMessage();
            // Передача меню в настройки
            option.MenuInlineKeyboardMarkup = testMenu;

            //Регистрация обработчика для последовательной обработки шагов и сохранение данных
            context.Update.RegisterStepHandler(new StepTelegram(DataFromUser, new StepCache()));
            await Helpers.Message.Send(context, context.Update, msg, option);
        }

        [InlineCallbackHandler<CustomTHeader>(CustomTHeader.AddDateForAddMoney)]
        // Добавляет дату фин.зачисления
        public async Task DataFromUser(IBotContext context)
        {
            try
            {
                string userInput;
                if (context.Update.Message != null)
                {
                    userInput = context.Update.Message.Text;
                    _sessionManager.AddMessageForDelete(context.GetChatId(), context.Update.Message);
                }
                else
                    userInput = DateTime.Now.ToString("dd.MM.yyyy");

                // Проверяем формат даты
                if (!DateTime.TryParseExact(
                    userInput,
                    "dd.MM.yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
                {
                    _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.Send(context, context.Update, "Ошибка: Неверный формат даты. Используйте ДД.ММ.ГГГГ"));
                    //await _navigationService.NavigateToMainMenu(context); // Возвращаем в главное меню

                    return; // Прерываем выполнение, ожидаем корректный ввод
                }
                
                var handler = context.Update.GetStepHandler<StepTelegram>();
                handler!.GetCache<StepCache>().DataFromUser = userInput;
                handler.RegisterNextStep(GoalFromUser);

                string msg = "Пришлите цель пополнения средств";
                await Helpers.Message.Send(context, context.Update, msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка добавления даты: {ex.Message}");
                _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.Send(context, context.Update, "Произошла ошибка. Попробуйте снова."));
                await _navigationService.NavigateToMainMenu(context); // Возвращаем в главное меню
            }
        }

        // Добавляет цель фин.зачисления
        public async Task GoalFromUser(IBotContext context)
        {
            // Получаем текущий обработчик
            var handler = context.Update.GetStepHandler<StepTelegram>();
            // Записываем текст пользователя в кэш 
            handler!.GetCache<StepCache>().GoalFromUser = context.Update.Message.Text;

            string msg = "Пришлите сумму пополнения, в формате ➡ 1500";

            //Регистрация следующего шага с максимальным ожиданием выполнения этого шага 5 минут от момента регистрации
            handler.RegisterNextStep(CoastFromUser);

            await Helpers.Message.Send(context, context.Update, msg);
        }

        // Добавляет сумму фин.зачисления
        public async Task CoastFromUser(IBotContext context)
        {
            var handler = context.Update.GetStepHandler<StepTelegram>();
            string userInput = context.Update.Message.Text;

            // Регулярное выражение для проверки формата суммы
            var regex = new Regex(@"^\d+([.,]\d{1,2})?$"); // Проверяем всю строку начиная с самого начала и до её конца (^..$)
                                                           // Должно быть число (0-9) один или более раз + 2 числа после разделителя
                                                           // Часть числа не обязательна и может повториться 0 или 1 раз благодаря "?"

            if (!regex.IsMatch(userInput)) // Дословно: проверь сообщение пользователя "userInput" на совпадения с форматом из "regex",
                                           // если совпадений нет, то отработай блок "if"
            {
                _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.Send(context, context.Update, "Ошибка: Сумма должна быть в формате '1500' или '1500.20'"));
                return; // Прерываем выполнение, ожидаем корректный ввод
            }

            // Нормализация суммы
            string normalizedSum = NormalizeSum(userInput);
            handler!.GetCache<StepCache>().CoastFromUser = normalizedSum;

            string msg = "Пришлите чек вложением в формате фото, или текстовое примечание";

            handler.RegisterNextStep(CreateNote);
            await Helpers.Message.Send(context, context.Update, msg);
        }

        // Собирает примечание
        public async Task CreateNote(IBotContext context)
        {
            var handler = context.Update.GetStepHandler<StepTelegram>();
            string msg = "Хорошо. Вношу данные в файл учёта средств";

            // Проверяем, прислал ли пользователь ТЕКСТ
            if (context.Update.Message?.Text != null)
            {
                handler!.GetCache<StepCache>().Note = context.Update.Message.Text;
            }
            // Проверяем, прислал ли пользователь ФОТО
            else if (context.Update.Message?.Photo != null && context.Update.Message.Photo.Any())
            {
                var largestPhoto = context.Update.Message.Photo.Last();

                try
                {
                    // Получаем файл из Telegram
                    var file = await context.BotClient.GetFile(largestPhoto.FileId);

                    // Формируем имя файла (уникальное, с датой и ID пользователя)
                    string fileName = $"check_{context.GetChatId()}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    string diskPath = $"User_{context.GetChatId()}/{fileName}"; // Путь на Яндекс.Диске

                    // Сохраняем файл временно на сервер
                    string localPath = Path.Combine(_settingsInstance.DocumentsDirectory, fileName);
                    using (var fileStream = new FileStream(localPath, FileMode.Create))
                    {
                        await context.BotClient.DownloadFile(file.FilePath, fileStream);
                    }

                    // Загружаем на Яндекс.Диск и получаем публичную ссылку
                    var diskService = new YandexDiskService(_yandexTokenService, new HttpClient());
                    string publicLink = await diskService.UploadFileAndGetPublicLinkAsync(localPath, diskPath);

                    // Сохраняем ссылку в кэш, как примечание
                    handler!.GetCache<StepCache>().Note = publicLink;

                    // Удаляем временный файл
                    File.Delete(localPath);
                }
                catch (Exception ex)
                {
                    _sessionManager.AddMessageForDelete(
                        context.GetChatId(),
                        await Helpers.Message.Send(context, context.Update, $"Ошибка загрузки чека: {ex.Message}" +
                        $"\n\nПопробуйте добавить текстовое примечание или фото чека в форматах JPEG и PNG")
                    );
                    handler!.RegisterNextStep(CreateNote);
                    return;
                }
            }

            await Helpers.Message.Send(context, context.Update, msg);
            await AddData(context);
        }

        // Добавляет все полученные данные в файл
        public async Task AddData(IBotContext context)
        {
            try
            {
                // Получаем текущий обработчик
                var handler = context.Update.GetStepHandler<StepTelegram>();

                // Устанавливаем бесплатную лицензию для некоммерческого использования
                ExcelPackage.License.SetNonCommercialPersonal("Vladimir");

                // Получаем путь к Excel файлу с данными
                string exclePath = _settingsInstance.GetExcelPath();

                // Открываем/создаем Excel файл
                FileInfo fileInfo = new FileInfo(exclePath);

                // Проверяем существование файла
                if (!fileInfo.Exists)
                {
                    _sessionManager.AddMessageForDelete(context.GetChatId(), await Helpers.Message.Send(context, context.Update, "Проверьте наличие отчёта, как файла ..."));
                    return; // Отменяем дальнейшее выполнение логики
                }

                using (var package = new ExcelPackage(fileInfo))
                {
                    // Включаем вычисление формул
                    package.Workbook.Calculate();

                    var worksheet = package.Workbook.Worksheets[0];

                    if (worksheet == null)
                    {
                        _sessionManager.AddMessageForDelete(context.GetChatId(), await Helpers.Message.Send(context, context.Update, "Ошибка: лист не найден"));
                        return;
                    }

                    // Находим первую пустую ячейку в столбце B
                    int emptyRow = 4; // начинаем с B4
                    int emptyCol = 2;
                    while (worksheet.Cells[emptyRow, emptyCol].Value != null && emptyRow <= 200) //Перебираем ячейки, пока не найдём пустую
                    {
                        emptyRow++;
                    }

                    // Записываем значения пользователя в ячейки
                    var dateCell = worksheet.Cells[emptyRow, emptyCol++]; // Ячейка для даты
                    if (DateTime.TryParse(handler.GetCache<StepCache>().DataFromUser, out DateTime parsedDate))
                    {
                        dateCell.Value = parsedDate; // Устанавливаем значение как DateTime
                        dateCell.Style.Numberformat.Format = "dd.mm.yyyy"; // Формат даты: день.месяц.год
                    }
                    else
                    {
                        dateCell.Value = handler.GetCache<StepCache>().DataFromUser; // Если не удалось распознать дату, сохраняем как текст
                    }
                    worksheet.Cells[emptyRow, emptyCol++].Value = handler.GetCache<StepCache>().GoalFromUser; // Записали цель

                    // Записываем значение суммы
                    var coastCell = worksheet.Cells[emptyRow, emptyCol++];
                    if (decimal.TryParse(handler.GetCache<StepCache>().CoastFromUser, out decimal coastValue))
                    {
                        coastCell.Value = coastValue;
                        coastCell.Style.Numberformat.Format = "# ₽"; // Финансовый формат с рублём
                    }
                    else
                    {
                        coastCell.Value = handler.GetCache<StepCache>().CoastFromUser; // Если не число, сохраняем как текст
                    }

                    // Добавление примечания
                    var userRecipt = handler.GetCache<StepCache>().Note;
                    if (!string.IsNullOrEmpty(userRecipt))
                    {
                        if (Uri.TryCreate(userRecipt, UriKind.Absolute, out Uri? result) &&
                            (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
                            worksheet.Cells[emptyRow, emptyCol++].Hyperlink = new ExcelHyperLink(userRecipt) { Display = "Скачать чек" };
                        else
                            worksheet.Cells[emptyRow, emptyCol++].Value = userRecipt;
                    }
                    else
                    {
                        _sessionManager.AddMessageForDelete(context.GetChatId(), await Helpers.Message.Send(context, "Чек недействителен"));
                    }

                    // Сохраняем файл
                    package.Save();

                    handler.GetCache<StepCache>().ClearData(); // Очищает кеш

                    handler.LastStepExecuted = true; // Последний шаг

                    await Helpers.Message.Send(context, context.Update, "Баланс пополнен");
                    await _navigationService.NavigateToMainMenu(context); // Возвращаем в главное меню
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка добавления даты: {ex.Message}");
            }
        }

        #endregion

        #region CustomTHeader.CostOfMaterials

        // Начинает пошаговую работу
        [AdminOnlyExample]
        [InlineCallbackHandler<CustomTHeader>(CustomTHeader.CostOfMaterials)]
        public async Task CostOfMaterials(IBotContext context)
        {
            context.Update.ClearCacheData();

            string msg = "Пришлите дату покупки материалов, в формате: ДД.ММ.ГГГГ" +
                "\nИли выберите сегодняшнюю дату 👇";

            string todayDate = DateTime.Now.ToString("dd.MM.yyyy"); // .ToString("dd.MM.yyyy") - преобразует дату в строку, используя указанный формат.

            // Строим меню
            var exampleItemOne = new InlineCallback($"{todayDate}", CustomTHeader.AddDateForPurchaseMaterials);

            // IInlineContent - реализуют все inline кнопки
            List<IInlineContent> menu = new();

            menu.Add(exampleItemOne);

            // Генерация меню на основе данных в * столбец
            var testMenu = MenuGenerator.InlineKeyboard(1, menu);

            // Создание настроек для передачи в сообщение
            var option = new OptionMessage();
            // Передача меню в настройки
            option.MenuInlineKeyboardMarkup = testMenu;

            //Регистрация обработчика для последовательной обработки шагов и сохранение данных
            context.Update.RegisterStepHandler(new StepTelegram(DataFromUser_Materials, new StepCache()));
            await Helpers.Message.Send(context, context.Update, msg, option);
        }

        [InlineCallbackHandler<CustomTHeader>(CustomTHeader.AddDateForPurchaseMaterials)]
        // Добавляет дату трат на материал
        public async Task DataFromUser_Materials(IBotContext context)
        {
            try
            {
                string userInput;
                if (context.Update.Message != null)
                {
                    userInput = context.Update.Message.Text;
                    _sessionManager.AddMessageForDelete(context.GetChatId(), context.Update.Message);
                }
                else
                    userInput = DateTime.Now.ToString("dd.MM.yyyy");

                // Проверяем формат даты
                if (!DateTime.TryParseExact(
                    userInput,
                    "dd.MM.yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
                {
                    _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.Send(context, context.Update, "Ошибка: Неверный формат даты. Используйте ДД.ММ.ГГГГ"));
                    await _navigationService.NavigateToMainMenu(context); // Возвращаем в главное меню
                    return;
                }
                
                var handler = context.Update.GetStepHandler<StepTelegram>();
                handler!.GetCache<StepCache>().DataFromUser = userInput;
                // Регистрация следующего шага с максимальным ожиданием выполнения этого шага 5 минут от момента регистрации
                handler.RegisterNextStep(GoalFromUser_Materials);

                string msg = "Пришлите наименование материала, на который были потрачены средства";

                await Helpers.Message.Send(context, context.Update, msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка добавления даты: {ex.Message}");
                _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.Send(context, context.Update, "Произошла ошибка. Попробуйте снова."));
                await _navigationService.NavigateToMainMenu(context); // Возвращаем в главное меню
            }
        }

        // Добавляет цель фин.зачисления
        public async Task GoalFromUser_Materials(IBotContext context)
        {
            // Получаем текущий обработчик
            var handler = context.Update.GetStepHandler<StepTelegram>();
            // Записываем текст пользователя в кэш 
            handler!.GetCache<StepCache>().GoalFromUser = context.Update.Message.Text;

            string msg = "Пришлите сумму расходов на материал";
            //Регистрация следующего шага с максимальным ожиданием выполнения этого шага 5 минут от момента регистрации
            handler.RegisterNextStep(CoastFromUser_Materials);

            await Helpers.Message.Send(context, context.Update, msg);
        }

        // Добавляет сумму фин.зачисления
        public async Task CoastFromUser_Materials(IBotContext context)
        {
            var handler = context.Update.GetStepHandler<StepTelegram>();
            string userInput = context.Update.Message.Text;

            // Регулярное выражение для проверки формата суммы
            var regex = new Regex(@"^\d+([.,]\d{1,2})?$");
            if (!regex.IsMatch(userInput))
            {
                _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.Send(context, context.Update, "Ошибка: Сумма должна быть в формате '1500' или '1500.20'"));
                return; // Прерываем выполнение, ожидаем корректный ввод
            }

            // Нормализация суммы
            string normalizedSum = NormalizeSum(userInput);
            handler!.GetCache<StepCache>().CoastFromUser = normalizedSum;

            string msg = "Пришлите чек вложением в формате фото, или текстовое примечание";

            //Регистрация следующего шага с максимальным ожиданием выполнения этого шага 5 минут от момента регистрации
            handler.RegisterNextStep(Note_Materials);

            await Helpers.Message.Send(context, context.Update, msg);
        }

        // Добавляет примечание фин.зачисления
        public async Task Note_Materials(IBotContext context)
        {
            var handler = context.Update.GetStepHandler<StepTelegram>();
            string msg = "Хорошо. Вношу данные в файл учёта средств";

            // Проверяем, прислал ли пользователь ТЕКСТ
            if (context.Update.Message?.Text != null)
            {
                handler!.GetCache<StepCache>().Note = context.Update.Message.Text;
            }
            // Проверяем, прислал ли пользователь ФОТО
            else if (context.Update.Message?.Photo != null && context.Update.Message.Photo.Any())
            {
                var largestPhoto = context.Update.Message.Photo.Last();

                try
                {
                    // Получаем файл из Telegram
                    var file = await context.BotClient.GetFile(largestPhoto.FileId);

                    // Формируем имя файла (уникальное, с датой и ID пользователя)
                    string fileName = $"check_{context.GetChatId()}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    string diskPath = $"User_{context.GetChatId()}/{fileName}"; // Путь на Яндекс.Диске

                    // Сохраняем файл временно на сервер
                    string localPath = Path.Combine(_settingsInstance.DocumentsDirectory, fileName);
                    using (var fileStream = new FileStream(localPath, FileMode.Create))
                    {
                        await context.BotClient.DownloadFile(file.FilePath, fileStream);
                    }

                    // Загружаем на Яндекс.Диск и получаем публичную ссылку
                    var diskService = new YandexDiskService(_yandexTokenService, new HttpClient());
                    string publicLink = await diskService.UploadFileAndGetPublicLinkAsync(localPath, diskPath);

                    // Сохраняем ссылку в кэш, как примечание
                    handler!.GetCache<StepCache>().Note = publicLink;

                    // Удаляем временный файл
                    File.Delete(localPath);
                }
                catch (Exception ex)
                {
                    _sessionManager.AddMessageForDelete(
                        context.GetChatId(),
                        await Helpers.Message.Send(context, context.Update, $"Ошибка загрузки чека: {ex.Message}" +
                        $"\n\nПопробуйте добавить текстовое примечание или фото чека в форматах JPEG и PNG")
                    );
                    return;
                }
            }

            await Helpers.Message.Send(context, context.Update, msg);

            //Выполнение внесения данных в файл
            await AddData_Materials(context);
        }

        // Добавляет все полученные данные в файл
        public async Task AddData_Materials(IBotContext context)
        {
            try
            {
                // Получаем текущий обработчик
                var handler = context.Update.GetStepHandler<StepTelegram>();

                // Устанавливаем бесплатную лицензию для некоммерческого использования
                ExcelPackage.License.SetNonCommercialPersonal("Vladimir");

                // Получаем путь к Excel файлу с данными
                string exclePath = _settingsInstance.GetExcelPath();

                // Открываем/создаем Excel файл
                FileInfo fileInfo = new FileInfo(exclePath);

                // Проверяем существование файла
                if (!fileInfo.Exists)
                {
                    _sessionManager.AddMessageForDelete(context.GetChatId(), await Helpers.Message.Send(context, context.Update, "Проверьте наличие отчёта, как файла ..."));
                    return; // Отменяем дальнейшее выполнение логики
                }

                using (var package = new ExcelPackage(fileInfo))
                {
                    // Включаем вычисление формул
                    package.Workbook.Calculate();

                    var worksheet = package.Workbook.Worksheets[0];

                    if (worksheet == null)
                    {
                        _sessionManager.AddMessageForDelete(context.GetChatId(), await Helpers.Message.Send(context, context.Update, "Ошибка: лист не найден"));
                        return;
                    }

                    // Находим первую пустую ячейку в столбце G
                    int emptyRow = 5; // начинаем с G5
                    int emptyCol = 7;
                    while (worksheet.Cells[emptyRow, emptyCol].Value != null && emptyRow <= 200) //Перебираем ячейки, пока не найдём пустую
                    {
                        emptyRow++;
                    }

                    // Записываем значения пользователя в ячейки
                    var dateCell = worksheet.Cells[emptyRow, emptyCol++]; // Ячейка для даты
                    if (DateTime.TryParse(handler.GetCache<StepCache>().DataFromUser, out DateTime parsedDate))
                    {
                        dateCell.Value = parsedDate; // Устанавливаем значение как DateTime
                        dateCell.Style.Numberformat.Format = "dd.mm.yyyy"; // Формат даты: день.месяц.год
                    }
                    else
                    {
                        dateCell.Value = handler.GetCache<StepCache>().DataFromUser; // Если не удалось распознать дату, сохраняем как текст
                    }

                    worksheet.Cells[emptyRow, emptyCol++].Value = handler.GetCache<StepCache>().GoalFromUser; // Записали цель

                    var coastCell = worksheet.Cells[emptyRow, emptyCol++]; // Для суммы
                    if (decimal.TryParse(handler.GetCache<StepCache>().CoastFromUser, out decimal coastValue))
                    {
                        coastCell.Value = coastValue;
                        coastCell.Style.Numberformat.Format = "# ₽"; // Финансовый формат с рублём
                    }
                    else
                    {
                        coastCell.Value = handler.GetCache<StepCache>().CoastFromUser; // Если не число, сохраняем как текст
                    }

                    // Добавление примечания
                    var userRecipt = handler.GetCache<StepCache>().Note;
                    if (!string.IsNullOrEmpty(userRecipt))
                    {
                        if (Uri.TryCreate(userRecipt, UriKind.Absolute, out Uri? result) &&
                            (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
                            worksheet.Cells[emptyRow, emptyCol++].Hyperlink = new ExcelHyperLink(userRecipt) { Display = "Скачать чек" };
                        else
                            worksheet.Cells[emptyRow, emptyCol++].Value = userRecipt;
                    }
                    else
                    {
                        _sessionManager.AddMessageForDelete(context.GetChatId(), await Helpers.Message.Send(context, "Чек недействителен"));
                    }

                    // Сохраняем файл
                    package.Save();

                    handler.GetCache<StepCache>().ClearData(); // Очищает кеш

                    handler.LastStepExecuted = true; // Последний шаг

                    await Helpers.Message.Send(context, context.Update, "Расходы на материал учтены.");

                    await _navigationService.NavigateToMainMenu(context); // Возвращаем в главное меню
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка добавления даты: {ex.Message}");
            }
        }

        #endregion

        #region CustomTHeader.LaborCosts

        // Начинает пошаговую работу
        [AdminOnlyExample]
        [InlineCallbackHandler<CustomTHeader>(CustomTHeader.LaborCosts)]
        public async Task LaborCosts(IBotContext context)
        {
            context.Update.ClearCacheData();

            string msg = "Пришлите дату оплаты работ, в формате: ДД.ММ.ГГГГ" +
                "\nИли выберите сегодняшнюю 👇";

            string todayDate = DateTime.Now.ToString("dd.MM.yyyy"); // .ToString("dd.MM.yyyy") - преобразует дату в строку, используя указанный формат.

            // Строим меню
            var exampleItemOne = new InlineCallback($"{todayDate}", CustomTHeader.AddDateForPurchaseMaterials);

            // IInlineContent - реализуют все inline кнопки
            List<IInlineContent> menu = new();

            menu.Add(exampleItemOne);

            // Генерация меню на основе данных в * столбец
            var testMenu = MenuGenerator.InlineKeyboard(1, menu);

            // Создание настроек для передачи в сообщение
            var option = new OptionMessage();
            // Передача меню в настройки
            option.MenuInlineKeyboardMarkup = testMenu;

            //Регистрация обработчика для последовательной обработки шагов и сохранение данных
            context.Update.RegisterStepHandler(new StepTelegram(DataFromUser_LaborCosts, new StepCache()));
            await Helpers.Message.Send(context, context.Update, msg, option);
        }

        // Добавляет дату фин.зачисления
        public async Task DataFromUser_LaborCosts(IBotContext context)
        {
            try
            {
                var handler = context.Update.GetStepHandler<StepTelegram>();

                string userInput;
                if (context.Update.Message != null)
                {
                    userInput = context.Update.Message.Text;
                    _sessionManager.AddMessageForDelete(context.GetChatId(), context.Update.Message);
                }
                else
                    userInput = DateTime.Now.ToString("dd.MM.yyyy");

                // Проверяем формат даты
                if (!DateTime.TryParseExact(
                    userInput,
                    "dd.MM.yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out _))
                {
                    _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.Send(context, context.Update, "Ошибка: Неверный формат даты. Используйте ДД.ММ.ГГГГ"));
                    await _navigationService.NavigateToMainMenu(context); // Возвращаем в главное меню
                    return;
                }

                // Если дата корректна - сохраняем в кэш
                handler!.GetCache<StepCache>().DataFromUser = userInput;

                string msg = "Пришлите наименование оплаченных работ";
                //Регистрация следующего шага с максимальным ожиданием выполнения этого шага 5 минут от момента регистрации
                handler.RegisterNextStep(GoalFromUser_LaborCosts);

                await Helpers.Message.Send(context, context.Update, msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка добавления даты: {ex.Message}");
                _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.Send(context, context.Update, "Произошла ошибка. Попробуйте снова."));
                await _navigationService.NavigateToMainMenu(context); // Возвращаем в главное меню
            }
        }

        // Добавляет цель фин.зачисления
        public async Task GoalFromUser_LaborCosts(IBotContext context)
        {
            // Получаем текущий обработчик
            var handler = context.Update.GetStepHandler<StepTelegram>();
            // Записываем текст пользователя в кэш 
            handler!.GetCache<StepCache>().GoalFromUser = context.Update.Message.Text;

            string msg = "Пришлите сумму стоимости работ";
            //Регистрация следующего шага с максимальным ожиданием выполнения этого шага 5 минут от момента регистрации
            handler.RegisterNextStep(CoastFromUser_LaborCosts);

            await Helpers.Message.Send(context, context.Update, msg);
        }

        // Добавляет сумму фин.зачисления
        public async Task CoastFromUser_LaborCosts(IBotContext context)
        {
            var handler = context.Update.GetStepHandler<StepTelegram>();
            string userInput = context.Update.Message.Text;

            // Регулярное выражение для проверки формата суммы
            var regex = new Regex(@"^\d+([.,]\d{1,2})?$");
            if (!regex.IsMatch(userInput))
            {
                _sessionManager.AddMessageForDelete(context.Update.GetChatId(), await Helpers.Message.Send(context, context.Update, "Ошибка: Сумма должна быть в формате '1500' или '1500.20'"));
                return; // Прерываем выполнение, ожидаем корректный ввод
            }

            // Нормализация суммы
            string normalizedSum = NormalizeSum(userInput);
            handler!.GetCache<StepCache>().CoastFromUser = normalizedSum;

            string msg = "Пришлите чек вложением в формате фото, или текстовое примечание";
            //Регистрация следующего шага с максимальным ожиданием выполнения этого шага 5 минут от момента регистрации
            handler.RegisterNextStep(Note_LaborCosts);

            await Helpers.Message.Send(context, context.Update, msg);
        }

        // Добавляет примечание фин.зачисления
        public async Task Note_LaborCosts(IBotContext context)
        {
            var handler = context.Update.GetStepHandler<StepTelegram>();
            string msg = "Хорошо. Вношу данные в файл учёта средств";

            // Проверяем, прислал ли пользователь ТЕКСТ
            if (context.Update.Message?.Text != null)
            {
                handler!.GetCache<StepCache>().Note = context.Update.Message.Text;
            }
            // Проверяем, прислал ли пользователь ФОТО
            else if (context.Update.Message?.Photo != null && context.Update.Message.Photo.Any())
            {
                var largestPhoto = context.Update.Message.Photo.Last();

                try
                {
                    // Получаем файл из Telegram
                    var file = await context.BotClient.GetFile(largestPhoto.FileId);

                    // Формируем имя файла (уникальное, с датой и ID пользователя)
                    string fileName = $"check_{context.GetChatId()}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg";
                    string diskPath = $"User_{context.GetChatId()}/{fileName}"; // Путь на Яндекс.Диске

                    // Сохраняем файл временно на сервер
                    string localPath = Path.Combine(_settingsInstance.DocumentsDirectory, fileName);
                    using (var fileStream = new FileStream(localPath, FileMode.Create))
                    {
                        await context.BotClient.DownloadFile(file.FilePath, fileStream);
                    }

                    // Загружаем на Яндекс.Диск и получаем публичную ссылку
                    var diskService = new YandexDiskService(_yandexTokenService, new HttpClient());
                    string publicLink = await diskService.UploadFileAndGetPublicLinkAsync(localPath, diskPath);

                    // Сохраняем ссылку в кэш, как примечание
                    handler!.GetCache<StepCache>().Note = publicLink;

                    // Удаляем временный файл
                    File.Delete(localPath);
                }
                catch (Exception ex)
                {
                    _sessionManager.AddMessageForDelete(
                        context.GetChatId(),
                        await Helpers.Message.Send(context, context.Update, $"Ошибка загрузки чека: {ex.Message}" +
                        $"\n\nПопробуйте добавить текстовое примечание или фото чека в форматах JPEG и PNG")
                    );
                    return;
                }
            }

            await Helpers.Message.Send(context, context.Update, msg);

            //Выполнение внесения данных в файл
            await AddData_LaborCosts(context);
        }

        // Добавляет все полученные данные в файл
        public async Task AddData_LaborCosts(IBotContext context)
        {
            try
            {
                // Получаем текущий обработчик
                var handler = context.Update.GetStepHandler<StepTelegram>();

                // Устанавливаем бесплатную лицензию для некоммерческого использования
                ExcelPackage.License.SetNonCommercialPersonal("Vladimir");

                // Получаем путь к Excel файлу с данными
                string exclePath = _settingsInstance.GetExcelPath();

                // Открываем/создаем Excel файл
                FileInfo fileInfo = new FileInfo(exclePath);

                // Проверяем существование файла
                if (!fileInfo.Exists)
                {
                    _sessionManager.AddMessageForDelete(context.GetChatId(), await Helpers.Message.Send(context, context.Update, "Проверьте наличие отчёта, как файла ..."));
                    return; // Отменяем дальнейшее выполнение логики
                }

                using (var package = new ExcelPackage(fileInfo))
                {
                    // Включаем вычисление формул
                    package.Workbook.Calculate();

                    var worksheet = package.Workbook.Worksheets[0];

                    if (worksheet == null)
                    {
                        _sessionManager.AddMessageForDelete(context.GetChatId(), await Helpers.Message.Send(context, context.Update, "Ошибка: лист не найден"));
                        return;
                    }

                    // Находим первую пустую ячейку в столбце L
                    int emptyRow = 4; // начинаем с L4
                    int emptyCol = 12;
                    while (worksheet.Cells[emptyRow, emptyCol].Value != null && emptyRow <= 200) //Перебираем ячейки, пока не найдём пустую
                    {
                        emptyRow++;
                    }

                    // Записываем значения пользователя в ячейки
                    var dateCell = worksheet.Cells[emptyRow, emptyCol++]; // Ячейка для даты
                    if (DateTime.TryParse(handler.GetCache<StepCache>().DataFromUser, out DateTime parsedDate))
                    {
                        dateCell.Value = parsedDate; // Устанавливаем значение как DateTime
                        dateCell.Style.Numberformat.Format = "dd.mm.yyyy"; // Формат даты: день.месяц.год
                    }
                    else
                    {
                        dateCell.Value = handler.GetCache<StepCache>().DataFromUser; // Если не удалось распознать дату, сохраняем как текст
                    }

                    worksheet.Cells[emptyRow, emptyCol++].Value = handler.GetCache<StepCache>().GoalFromUser; // Записали цель

                    var coastCell = worksheet.Cells[emptyRow, emptyCol++]; // Для суммы
                    if (decimal.TryParse(handler.GetCache<StepCache>().CoastFromUser, out decimal coastValue))
                    {
                        coastCell.Value = coastValue;
                        coastCell.Style.Numberformat.Format = "# ₽"; // Финансовый формат с рублём
                    }
                    else
                    {
                        coastCell.Value = handler.GetCache<StepCache>().CoastFromUser; // Если не число, сохраняем как текст
                    }

                    // Добавление примечания
                    var userRecipt = handler.GetCache<StepCache>().Note;
                    if (!string.IsNullOrEmpty(userRecipt))
                    {
                        if (Uri.TryCreate(userRecipt, UriKind.Absolute, out Uri? result) &&
                            (result.Scheme == Uri.UriSchemeHttp || result.Scheme == Uri.UriSchemeHttps))
                            worksheet.Cells[emptyRow, emptyCol++].Hyperlink = new ExcelHyperLink(userRecipt) { Display = "Скачать чек" };
                        else
                            worksheet.Cells[emptyRow, emptyCol++].Value = userRecipt;
                    }
                    else
                    {
                        _sessionManager.AddMessageForDelete(context.GetChatId(), await Helpers.Message.Send(context, "Чек недействителен"));
                    }

                    // Сохраняем файл
                    package.Save();

                    handler.GetCache<StepCache>().ClearData(); // Очищает кеш

                    handler.LastStepExecuted = true; // Последний шаг

                    await Helpers.Message.Send(context, context.Update, "Расходы на оплату труда - учтены.");
                    await _navigationService.NavigateToMainMenu(context); // Возвращаем в главное меню
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка добавления даты: {ex.Message}");
            }
        }
        #endregion

        #region Утлиты
        // Приводит сумму от пользователя к нужному формату
        private string NormalizeSum(string input)
        {
            // Заменяем запятую на точку
            string normalized = input.Replace(',', '.');

            // Удаляем все символы, кроме цифр и точки
            normalized = new string(normalized.Where(c => char.IsDigit(c) || c == '.').ToArray());

            // Удаляем лишние точки (оставляем только одну, если она есть)
            normalized = Regex.Replace(normalized, @"\.{2,}", "."); // Удаляем повторяющиеся точки
            normalized = normalized.Trim('.'); // Удаляем точки в начале/конце

            // Парсим с явным указанием культуры
            if (decimal.TryParse(
                normalized,
                NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal sum))
            {
                // Округляем до целого числа и возвращаем без десятичных знаков
                return Math.Round(sum).ToString("0", CultureInfo.InvariantCulture);
            }

            return "0"; // Значение по умолчанию при ошибке
        }

        #endregion
    }
}


