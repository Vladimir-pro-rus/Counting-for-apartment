using PRTelegramBot.Attributes;
using System.ComponentModel;

namespace ConsoleExample.Models.CommandHeaders
{
    [InlineCommand]
    public enum CustomTHeader
    {
        #region Rudements
        [Description("Бесплатный ВИП")]
        GetFreeVIP = 500,
        [Description("Вип на 1 день")]
        GetVipOneDay,
        [Description("Вип на 1 неделю")]
        GetVipOneWeek,
        [Description("Вип на 1 месяц")]
        GetVipOneMonth,
        [Description("Вип навсегда")]
        GetVipOneForever,
        [Description("Шаг из Inline")]
        InlineWithStep,
        [Description("Кастомная кнопка")]
        CustomButton,
        [Description("Callback для календаря")]
        CalendarCallback,
        #endregion
       
        [Description("Главное меню")]
        MainMenu,

        [Description("Создаёт меню материала к выдаче")]
        ChechkingBalance,
        
        [Description("Позволняет взаимодействовать с данными файла")]
        EditBalance,

        [Description("Добавляет поступившие средства")]
        AddMoney,
        [Description("Фиксирует траты на материал")]
        CostOfMaterials,
        [Description("Фиксирует траты на оплату труда")]
        LaborCosts,

        [Description("Отправляет файл эксель пользователю")]
        TakeFile,
        
        [Description("Добавить дату из кнопки")]
        AddDateForAddMoney,
        AddDateForPurchaseMaterials,
    }
}
