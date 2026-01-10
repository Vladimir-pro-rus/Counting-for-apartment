using PRTelegramBot.Attributes;
using System.ComponentModel;

namespace ConsoleExample.Models.CommandHeaders
{
    [InlineCommand]
    public enum CustomTHeader
    {      
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
