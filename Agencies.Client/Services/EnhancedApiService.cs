using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Agencies.Client.Services
{
    public class EnhancedApiService : ApiService, IDisposable
    {
        private readonly IErrorHandler _errorHandler;
        private HttpClient _httpClient;
        private bool _disposed;

        public EnhancedApiService(IErrorHandler errorHandler, string baseUrl = "https://localhost:7149/api/")
            : base(baseUrl)
        {
            _errorHandler = errorHandler;
            _httpClient = CreateHttpClient(baseUrl);
        }

        private HttpClient CreateHttpClient(string baseUrl)
        {
            var client = new HttpClient
            {
                BaseAddress = new Uri(baseUrl)
            };

            // Настройка заголовков
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

            // Добавляем заголовки авторизации, если они есть в базовом классе
            TryAddAuthorizationHeader(client);

            return client;
        }

        private void TryAddAuthorizationHeader(HttpClient client)
        {
            // Попытка получить токен из базового класса, если там есть такая возможность
            // Это зависит от реализации базового класса ApiService
            var token = GetTokenFromBaseClass();
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        private string GetTokenFromBaseClass()
        {
            // Если в базовом классе есть метод для получения токена
            // Например: return base.GetAuthToken();
            // Или используем рефлексию
            try
            {
                var tokenProperty = typeof(ApiService).GetProperty("AuthToken",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (tokenProperty != null)
                {
                    return tokenProperty.GetValue(this) as string;
                }

                var tokenField = typeof(ApiService).GetField("_authToken",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (tokenField != null)
                {
                    return tokenField.GetValue(this) as string;
                }
            }
            catch
            {
                // Игнорируем ошибки рефлексии
            }

            return null;
        }

        public async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries = 3)
        {
            var retryCount = 0;
            while (true)
            {
                try
                {
                    return await action();
                }
                catch (HttpRequestException ex) when (retryCount < maxRetries)
                {
                    retryCount++;
                    await Task.Delay(1000 * retryCount); // Exponential backoff
                    _errorHandler.LogWarning($"Retry {retryCount}/{maxRetries}: {ex.Message}");
                }
                catch (Exception ex)
                {
                    _errorHandler.LogError(ex, "API request failed");
                    throw;
                }
            }
        }

        public new async Task<T> GetAsync<T>(string endpoint)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var response = await _httpClient.GetAsync(endpoint);
                await EnsureSuccessStatusCode(response);

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(json);
            });
        }

        public new async Task<T> PostAsync<T>(string endpoint, object data)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(endpoint, content);
                await EnsureSuccessStatusCode(response);

                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(responseJson);
            });
        }

        public new async Task<T> PutAsync<T>(string endpoint, object data)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var json = JsonConvert.SerializeObject(data);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PutAsync(endpoint, content);
                await EnsureSuccessStatusCode(response);

                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<T>(responseJson);
            });
        }

        public new async Task<bool> DeleteAsync(string endpoint)
        {
            return await ExecuteWithRetryAsync(async () =>
            {
                var response = await _httpClient.DeleteAsync(endpoint);
                await EnsureSuccessStatusCode(response);
                return response.IsSuccessStatusCode;
            });
        }

        private async Task EnsureSuccessStatusCode(HttpResponseMessage response)
        {
            if (response.IsSuccessStatusCode)
                return;

            var content = await response.Content.ReadAsStringAsync();
            ApiError error = null;

            try
            {
                error = JsonConvert.DeserializeObject<ApiError>(content);
            }
            catch
            {
                // Если не удалось десериализовать ошибку
            }

            throw new ApiException(
                error?.Message ?? $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                response.StatusCode,
                error?.ErrorCode,
                error?.Errors
            );
        }

        // Метод для обновления токена авторизации
        public void UpdateAuthorizationToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Remove("Authorization");
            }
            else
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        // Метод для сброса подключения
        public void ResetConnection()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EnhancedApiService));

            var oldClient = _httpClient;
            _httpClient = CreateHttpClient(_httpClient.BaseAddress.ToString());
            oldClient?.Dispose();
        }

        // Реализация IDisposable
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _httpClient?.Dispose();
                }
                _disposed = true;
            }
        }

        // Финализатор на всякий случай
        ~EnhancedApiService()
        {
            Dispose(false);
        }
    }

    public interface IErrorHandler
    {
        void LogError(Exception ex, string message);
        void LogWarning(string message);
        void ShowError(string message, string title = "Error");
    }

    public class ApiException : Exception
    {
        public HttpStatusCode StatusCode { get; }
        public string ErrorCode { get; }
        public Dictionary<string, string[]> Errors { get; }

        public ApiException(string message, HttpStatusCode statusCode, string errorCode = null,
            Dictionary<string, string[]> errors = null)
            : base(message)
        {
            StatusCode = statusCode;
            ErrorCode = errorCode;
            Errors = errors;
        }
    }

    public class ApiError
    {
        public bool Success { get; set; }
        public string Message { get; set; }
        public string ErrorCode { get; set; }
        public Dictionary<string, string[]> Errors { get; set; }
        public DateTime Timestamp { get; set; }
    }
}