using PRTelegramBot.Interfaces;

namespace ConsoleExample.Models
{
    /// <summary>
    /// Кэш для пошагового выполнения команд
    /// </summary>
    public class StepCache : ITelegramCache
    {
        #region Rudements
        public string Name { get; set; }
        public string BirthDay { get; set; }
        public bool ClearData()
        {
            this.BirthDay = string.Empty; 
            this.Name = string.Empty;
            return true;
        }
        #endregion

        public string Balance { get; set; } //BalanceService для пользователя
        public string DataFromUser { get; set; } //Данные от пользователя
        public string GoalFromUser { get; set; } //Цель от пользователя
        public string CoastFromUser { get; set; } //Сумма от пользователя
        public string Note { get; set; } //Примечание от пользователя

        public string WritingCell { get; set; } //Ячейка для записи
    }


}
