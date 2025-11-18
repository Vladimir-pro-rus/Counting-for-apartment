using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConsoleExample.Examples.Commands.AccountingOfFunds_bot
{
    internal class FilePathsSettings
    {
        public const string SectionName = "AppSettings"; // Константа для указания секции в конфигурации

        public string DocumentsDirectory { get; set; } = string.Empty; // Инициализируем пустой строкой
        public string PictureFileName { get; set; } = string.Empty;
        public string ExcelFileName { get; set; } = string.Empty;

        // Методы для получения полных путей
        public string GetPicturePath() => Path.Combine(DocumentsDirectory, PictureFileName);
        public string GetExcelPath() => Path.Combine(DocumentsDirectory, ExcelFileName);

    }
}
