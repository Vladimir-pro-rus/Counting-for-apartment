using PRTelegramBot.Interfaces;

namespace ConsoleExample.Models
{
    /// <summary>
    /// Кэш для пошагового выполнения команд
    /// </summary>
    public class StepCache : ITelegramCache
    {
        public string Balance { get; set; } // Баланс для пользователя
        public string DataFromUser { get; set; } // Данные от пользователя
        public string GoalFromUser { get; set; } // Цель от пользователя
        public string CoastFromUser { get; set; } // Сумма от пользователя
        public string Note { get; set; } // Примечание от пользователя

        public string WritingCell { get; set; } // Ячейка для записи в эксель
    }


}
