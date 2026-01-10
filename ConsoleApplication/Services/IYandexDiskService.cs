using PRTelegramBot.Attributes;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ConsoleExample.Services
{
    public interface IYandexDiskService
    {
        // Интерфейс объявляет, что будет такая реализация класс с этими методами и параметрами
        // Получает: путь до файла локально, путь загрузки на диск (в будующем запрос) и токен, для работы яндекс сервисов.
        Task<string> UploadFileAndGetPublicLinkAsync(string localFilePath, string diskPath, CancellationToken ct = default);
    }

    // Класс, реализующий запись файла на диск и формирования из него ссылки
    [BotHandler]
    public class YandexDiskService : IYandexDiskService
    {
        private readonly IYandexTokenService _tokenService;
        private readonly HttpClient _httpClient;

        // Конструктор, инициализирующий сервис
        public YandexDiskService(IYandexTokenService tokenService, HttpClient httpClient) // Получает "IYandexTokenService", путём реализации интерфейса при его инициализации
        {
            _tokenService = tokenService;
            _httpClient = httpClient;
            _httpClient.BaseAddress = new Uri("https://cloud-api.yandex.net/");
        }

        /// <summary>
        /// Загружает файл на Яндекс Диск и возвращает публичную ссылку.
        /// Автоматически создаёт родительскую папку, если её нет.
        /// </summary>
        public async Task<string> UploadFileAndGetPublicLinkAsync(
            string localFilePath,
            string diskPath,
            CancellationToken ct = default)
        {
            // Получаем токен доступа
            var accessToken = await _tokenService.GetAccessTokenAsync();

            // Устанавливаем заголовок авторизации для всех запросов
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("OAuth", accessToken);

            // Извлекаем родительскую директорию из пути (например, "/checks" из "/checks/file.jpg")
            string parentDir = Path.GetDirectoryName(diskPath);

            // Проверяем существование родительской папки на Яндекс Диске
            var checkDirResponse = await _httpClient.GetAsync(
                $"/v1/disk/resources?path={Uri.EscapeDataString(parentDir)}", ct);

            if (!checkDirResponse.IsSuccessStatusCode)
            {
                // Если папка не существует — создаём её
                var createDirRequest = new HttpRequestMessage(
                    HttpMethod.Put,
                    $"/v1/disk/resources?path={Uri.EscapeDataString(parentDir)}");

                createDirRequest.Headers.Authorization =
                    new AuthenticationHeaderValue("OAuth", accessToken);

                var createDirResponse = await _httpClient.SendAsync(createDirRequest, ct);
                createDirResponse.EnsureSuccessStatusCode(); // Бросает исключение при ошибке
            }

            // Запрашиваем временную ссылку для загрузки файла
            var uploadLinkResponse = await _httpClient.GetAsync(
                $"/v1/disk/resources/upload?path={Uri.EscapeDataString(diskPath)}&overwrite=true", ct);

            if (!uploadLinkResponse.IsSuccessStatusCode)
                throw new Exception($"Ошибка получения ссылки для загрузки: {await uploadLinkResponse.Content.ReadAsStringAsync(ct)}");

            // Получение публичной ссылки и её десериализация из Json формата в текстовый string
            var uploadLinkContent = await uploadLinkResponse.Content.ReadAsStringAsync(ct);
            var uploadLink = JsonSerializer.Deserialize<UploadLinkResponse>(uploadLinkContent)?.Href;

            if (string.IsNullOrEmpty(uploadLink))
                throw new Exception("Не удалось получить ссылку для загрузки");

            // Загружаем файл по полученной ссылке
            using var fileStream = File.OpenRead(localFilePath);
            using var content = new StreamContent(fileStream);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var uploadResult = await _httpClient.PutAsync(uploadLink, content, ct);

            if (!uploadResult.IsSuccessStatusCode)
                throw new Exception($"Ошибка загрузки файла: {await uploadResult.Content.ReadAsStringAsync(ct)}");

            // Публикуем файл (делаем доступным по публичной ссылке)
            var publishResponse = await _httpClient.PutAsync(
                $"/v1/disk/resources/publish?path={Uri.EscapeDataString(diskPath)}", null, ct);

            if (!publishResponse.IsSuccessStatusCode && publishResponse.StatusCode != System.Net.HttpStatusCode.NotFound)
                throw new Exception($"Ошибка публикации файла: {await publishResponse.Content.ReadAsStringAsync(ct)}");

            // Получаем информацию о файле, включая публичную ссылку (public из ответа)
            var resourceResponse = await _httpClient.GetAsync(
                $"/v1/disk/resources?path={Uri.EscapeDataString(diskPath)}", ct);

            // Работа кода ниже выглядит так:
            // 1) JsonSerializer анализирует JSON. 
            // 2) Находит поле "href".
            // 3) Сопоставляет его с свойством Href в классе UploadLinkResponse(благодаря[JsonPropertyName("href")]).
            // 4) Создаёт объект UploadLinkResponse и заполняет Href значением из JSON.
            // 5) Возвращает объект, из которого мы берём.Href.
            var resourceContent = await resourceResponse.Content.ReadAsStringAsync(ct);
            var resource = JsonSerializer.Deserialize<DiskResourceResponse>(resourceContent);

            return resource?.PublicUrl ?? throw new Exception("Публичная ссылка не получена");
        }

        // Принимает JSON‑ответ от метода /v1/disk/resources/upload (запрос временной ссылки для загрузки);
        // извлекает поле href из JSON и сохраняет в свойство Href.
        private class UploadLinkResponse
        {
            [JsonPropertyName("href")] // явно указывает, какое JSON‑поле соответствует свойству .NET.
            public string Href { get; set; } = string.Empty;
        }

        // Принимает JSON‑ответ от метода /v1/disk/resources (информация о файле/папке);
        // извлекает поле public_url из JSON и сохраняет в свойство PublicUrl.
        private class DiskResourceResponse
        {
            [JsonPropertyName("public_url")] // явно указывает, какое JSON‑поле соответствует свойству .NET.
            public string PublicUrl { get; set; } = string.Empty;
        }
    }

}
