
using Agencies.Core.DTO;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Agencies.API.Services
{
    public interface IReportsService
    {
        Task<SalesReportDto> GetSalesReportAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<PropertyAnalysisReportDto> GetPropertyAnalysisReportAsync();
        Task<List<AgentPerformanceDto>> GetAgentPerformanceReportAsync(DateTime? startDate = null, DateTime? endDate = null);
        Task<ClientAnalysisReportDto> GetClientAnalysisReportAsync();
        Task<FinancialReportDto> GetFinancialReportAsync(DateTime? startDate = null, DateTime? endDate = null);
    }

    public class ReportsService : IReportsService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public ReportsService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _baseUrl = configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7149/api/";

            // Добавьте заголовки авторизации если нужно
            var token = configuration["Token"];
            if (!string.IsNullOrEmpty(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        }

        public async Task<SalesReportDto> GetSalesReportAsync(DateTime? startDate = null, DateTime? endDate = null)
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

        public async Task<PropertyAnalysisReportDto> GetPropertyAnalysisReportAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}reports/property-analysis");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<PropertyAnalysisReportDto>(json);
        }

        public async Task<List<AgentPerformanceDto>> GetAgentPerformanceReportAsync(DateTime? startDate = null, DateTime? endDate = null)
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

        public async Task<ClientAnalysisReportDto> GetClientAnalysisReportAsync()
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}reports/client-analysis");
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<ClientAnalysisReportDto>(json);
        }

        public async Task<FinancialReportDto> GetFinancialReportAsync(DateTime? startDate = null, DateTime? endDate = null)
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
    }
}