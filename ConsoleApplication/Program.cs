using ConsoleExample.Examples.Commands.AccountingOfFunds_bot;
using ConsoleExample.Examples.InlineClassHandlers;
using ConsoleExample.Models.CommandHeaders;
using ConsoleExample.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRTelegramBot.Core;
using PRTelegramBot.Extensions;
using PRTelegramBot.Models.EventsArgs;

Console.WriteLine("Запуск программы");


// Создаём IConfiguration (настройки)
var configuration = new ConfigurationBuilder()
    .AddJsonFile("Configs/appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Создаём ServiceCollection и регистрируем сервисы в нём
var services = new ServiceCollection();

// Регистрируем IConfiguration (настройки)
services.AddSingleton<IConfiguration>(configuration);

// Регистрируем FilePathsSettings как IOptions<FilePathsSettings> (выносим путь в отд. переменную)
services.Configure<FilePathsSettings>(configuration.GetSection(FilePathsSettings.SectionName));

// Регистрируем IUserSessionManager как Singleton (один экземпляр на всё приложение)
services.AddSingleton<IUserSessionManager, UserSessionManagerImpl>();

//  Регистрируем INavigationService как Transient (новый экземпляр для каждой команды)
services.AddTransient<INavigationService, NavigationService>();

services.AddBotHandlers(); // Заменяет ручную регистрацию методов и классов в DI. Автоматизация от PRTelegram

// Регистрируем обработчик inline класса
services.AddTransient<InlineDefaultClassHandler>();

// Создаём IServiceProvider (поставщик сервисов)
var serviceProvider = services.BuildServiceProvider(); // BuildServiceProvider() — это команда, которая собирает всё, что зарегистрировано в
                                                       // services (типы, зависимости, как создавать), и создаёт из этого "машину" (объект),
                                                       // которая сможет предоставлять (поставлять) нужные объекты по требованию.
                                                       // Вот этот созданный объект и называется ServiceProvider.

var telegram = new PRBotBuilder("token")
                    .SetBotId(0)
                    .AddConfigPaths(Initializer.GetConfigPaths())
                    .AddAdmin(815434934)
                    .SetClearUpdatesOnStart(true)
                    .AddReplyDynamicCommands(Initializer.GetDynamicCommands())
                    .AddCommandChecker(Initializer.GetCommandChekers())
                    //.AddMiddlewares(new OneMiddleware(), new TwoMiddleware(), new ThreeMiddleware()) // Обработчики промежуточного нажатия
                    .AddInlineClassHandler(ClassTHeader.DefaultTestClass, typeof(InlineDefaultClassHandler))
                    .SetServiceProvider(serviceProvider) // Передаём "IServiceProvider" для создания экземпляров обработчиков (DI)
                    .Build();

// Инициализация событий для бота.
Initializer.InitEvents(telegram);
Initializer.InitLogEvents(telegram);
Initializer.InitMessageEvents(telegram);
Initializer.InitUpdateEvents(telegram);

// Инициализация новых команд для бота.
Initializer.InitCommands(telegram);

// Запуск работы бота.
await telegram.StartAsync();


telegram.Events.OnErrorLog += Events_OnErrorLog;

async Task Events_OnErrorLog(ErrorLogEventArgs arg)
{
    Console.WriteLine(arg.Exception.Message);
}

// Предотвращает закрытие консоли
while (true)
{
    var result = Console.ReadLine();
    if (result.Equals("exit", StringComparison.OrdinalIgnoreCase))
        Environment.Exit(0);
}