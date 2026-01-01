using Agencies.Core.DTO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Linq;

namespace Agencies.Client.Services
{
    public class ApiService
    {
        private readonly HttpClient _httpClient;
        private string _baseUrl;
        private string _token;

        public ApiService(string baseUrl = "https://localhost:7149/api/")
        {
            _baseUrl = baseUrl;

            // Разрешаем недоверенные сертификаты (только для разработки!)
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // Увеличиваем таймаут
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public void SetToken(string token)
        {
            _token = token;
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        public void ClearToken()
        {
            _token = null;
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }

        // Auth methods
        public async Task<LoginResponse> LoginAsync(LoginRequest request)
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}auth/login", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<LoginResponse>(responseJson);
        }

        public async Task<bool> RegisterAsync(RegisterRequest request)
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}auth/register", content);
            return response.IsSuccessStatusCode;
        }

        // Property methods
        public async Task<List<PropertyDto>> GetPropertiesAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}properties");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<PropertyDto>>(json);
        }

        public async Task<PropertyDto> GetPropertyAsync(int id)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}properties/{id}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<PropertyDto>(json);
        }

        public async Task<PropertyDto> CreatePropertyAsync(CreatePropertyRequest request)
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}properties", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<PropertyDto>(responseJson);
        }

        public async Task<PropertyDto> UpdatePropertyAsync(int id, UpdatePropertyRequest request)
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"{_baseUrl}properties/{id}", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<PropertyDto>(responseJson);
        }

        public async Task<bool> DeletePropertyAsync(int id)
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}properties/{id}");
            return response.IsSuccessStatusCode;
        }

        // Client methods
        public async Task<List<ClientDto>> GetClientsAsync(string search = "")
        {
            var url = string.IsNullOrEmpty(search)
                ? $"{_baseUrl}clients"
                : $"{_baseUrl}clients?search={Uri.EscapeDataString(search)}";

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<ClientDto>>(json);
        }

        public async Task<ClientDto> GetClientAsync(int id)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}clients/{id}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ClientDto>(json);
        }

        public async Task<ClientDto> CreateClientAsync(CreateClientRequest request)
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}clients", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ClientDto>(responseJson);
        }

        public async Task<ClientDto> UpdateClientAsync(int id, UpdateClientRequest request)
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"{_baseUrl}clients/{id}", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ClientDto>(responseJson);
        }

        public async Task<bool> DeleteClientAsync(int id)
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}clients/{id}");
            return response.IsSuccessStatusCode;
        }

        // Deal methods
        public async Task<List<DealDto>> GetDealsAsync(string status = "", string search = "")
        {
            var url = $"{_baseUrl}deals";
            var queryParams = new List<string>();

            if (!string.IsNullOrEmpty(status))
                queryParams.Add($"status={Uri.EscapeDataString(status)}");
            if (!string.IsNullOrEmpty(search))
                queryParams.Add($"search={Uri.EscapeDataString(search)}");

            if (queryParams.Any())
                url += "?" + string.Join("&", queryParams);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<DealDto>>(json);
        }

        public async Task<DealDto> GetDealAsync(int id)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}deals/{id}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<DealDto>(json);
        }

        public async Task<DealDto> CreateDealAsync(CreateDealRequest request)
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}deals", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<DealDto>(responseJson);
        }

        public async Task<DealDto> UpdateDealAsync(int id, UpdateDealRequest request)
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"{_baseUrl}deals/{id}", content);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<DealDto>(responseJson);
        }

        public async Task<bool> DeleteDealAsync(int id)
        {
            var response = await _httpClient.DeleteAsync($"{_baseUrl}deals/{id}");
            return response.IsSuccessStatusCode;
        }

        public async Task<object> GetDealStatisticsAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var url = $"{_baseUrl}deals/statistics";
            var queryParams = new List<string>();

            if (startDate.HasValue)
                queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
            if (endDate.HasValue)
                queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");

            if (queryParams.Any())
                url += "?" + string.Join("&", queryParams);

            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<object>(json);
        }

        // Report methods
        public async Task<string> GetReportAsync(string reportType)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}reports/{reportType}");
            response.EnsureSuccessStatusCode();

            return await response.Content.ReadAsStringAsync();
        }

        // User profile methods
        public async Task<UserDto> GetCurrentUserAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}users/me");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<UserDto>(json);
        }

        public async Task<bool> UpdateUserProfileAsync(UpdateUserRequest request)
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync($"{_baseUrl}users/profile", content);
            return response.IsSuccessStatusCode;
        }

        // Добавьте эти методы в конец класса ApiService:

        // Report methods
        public async Task<SalesReportDto> GetSalesReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var url = $"{_baseUrl}reports/sales";
                var queryParams = new List<string>();

                if (startDate.HasValue)
                    queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
                if (endDate.HasValue)
                    queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");

                if (queryParams.Any())
                    url += "?" + string.Join("&", queryParams);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<SalesReportDto>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting sales report: {ex.Message}");
                throw;
            }
        }

        public async Task<PropertyAnalysisReportDto> GetPropertyAnalysisReportAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}reports/property-analysis");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<PropertyAnalysisReportDto>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting property analysis: {ex.Message}");
                throw;
            }
        }

        public async Task<List<AgentPerformanceDto>> GetAgentPerformanceReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var url = $"{_baseUrl}reports/agent-performance";
                var queryParams = new List<string>();

                if (startDate.HasValue)
                    queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
                if (endDate.HasValue)
                    queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");

                if (queryParams.Any())
                    url += "?" + string.Join("&", queryParams);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<AgentPerformanceDto>>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting agent performance: {ex.Message}");
                throw;
            }
        }

        public async Task<ClientAnalysisReportDto> GetClientAnalysisReportAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}reports/client-analysis");
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<ClientAnalysisReportDto>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting client analysis: {ex.Message}");
                throw;
            }
        }

        public async Task<FinancialReportDto> GetFinancialReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var url = $"{_baseUrl}reports/financial";
                var queryParams = new List<string>();

                if (startDate.HasValue)
                    queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
                if (endDate.HasValue)
                    queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");

                if (queryParams.Any())
                    url += "?" + string.Join("&", queryParams);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<FinancialReportDto>(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting financial report: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> CheckConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}health");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetConnectionStatus()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}health");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    return $"Подключено к API. Ответ: {content}";
                }
                return $"API ответил с ошибкой: {response.StatusCode}";
            }
            catch (Exception ex)
            {
                return $"Ошибка подключения: {ex.Message}";
            }
        }

        public async Task<List<UserDto>> GetAgentsAsync()
        {
            try
            {
                Console.WriteLine($"[GetAgentsAsync] Начало. BaseUrl: {_baseUrl}");
                Console.WriteLine($"[GetAgentsAsync] Токен авторизации: {_httpClient.DefaultRequestHeaders.Authorization != null}");

                var url = $"{_baseUrl}users/agents";
                Console.WriteLine($"[GetAgentsAsync] Отправка запроса на: {url}");

                var response = await _httpClient.GetAsync(url);

                Console.WriteLine($"[GetAgentsAsync] Ответ получен. Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[GetAgentsAsync] JSON ответ: {json}");

                    var agents = JsonConvert.DeserializeObject<List<UserDto>>(json);
                    Console.WriteLine($"[GetAgentsAsync] Десериализовано агентов: {agents?.Count ?? 0}");

                    return agents ?? new List<UserDto>();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine($"[GetAgentsAsync] Доступ запрещен (403)");
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[GetAgentsAsync] Ошибка: {errorContent}");
                    throw new HttpRequestException("Доступ запрещен", null, response.StatusCode);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"[GetAgentsAsync] Ошибка HTTP {response.StatusCode}: {errorContent}");
                    throw new HttpRequestException($"HTTP ошибка: {response.StatusCode}", null, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"🔴 [GetAgentsAsync] Исключение: {ex.Message}");
                Console.WriteLine($"🔴 [GetAgentsAsync] StackTrace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Получить пользователя по ID
        /// </summary>
        public async Task<UserDto> GetUserByIdAsync(int id)
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}users/{id}");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<UserDto>(json);
        }

        /// <summary>
        /// Получить всех пользователей (только для админов)
        /// </summary>
        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}users");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<List<UserDto>>(json);
        }
    }
}