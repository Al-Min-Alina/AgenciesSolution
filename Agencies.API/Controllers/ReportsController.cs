using Agencies.Core.DTO;
using Agencies.Domain.Models;
using Agencies.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Agencies.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "AdminOrUser")]
    public class ReportsController : ControllerBase
    {
        private readonly IDealRepository _dealRepository;
        private readonly IPropertyRepository _propertyRepository;
        private readonly IClientRepository _clientRepository;
        private readonly IUserRepository _userRepository;

        public ReportsController(
            IDealRepository dealRepository,
            IPropertyRepository propertyRepository,
            IClientRepository clientRepository,
            IUserRepository userRepository)
        {
            _dealRepository = dealRepository;
            _propertyRepository = propertyRepository;
            _clientRepository = clientRepository;
            _userRepository = userRepository;
        }

        [HttpGet("sales")]
        public async Task<ActionResult<SalesReportDto>> GetSalesReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            // Устанавливаем даты по умолчанию
            if (!startDate.HasValue)
                startDate = DateTime.Now.AddMonths(-6);
            if (!endDate.HasValue)
                endDate = DateTime.Now;

            var deals = await _dealRepository.GetDealsByDateRangeAsync(startDate.Value, endDate.Value);

            // Фильтруем по пользователю если не админ
            if (userRole != "Admin")
            {
                deals = deals.Where(d => d.AgentId == userId);
            }

            var report = new SalesReportDto
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                GeneratedDate = DateTime.UtcNow,
                TotalDeals = deals.Count(),
                CompletedDeals = deals.Count(d => d.Status == "Завершено"),
                PendingDeals = deals.Count(d => d.Status == "В ожидании"),
                CancelledDeals = deals.Count(d => d.Status == "Отменено"),
                TotalRevenue = deals.Where(d => d.Status == "Завершено").Sum(d => d.DealAmount),
                AverageDealAmount = deals.Any() ? deals.Average(d => d.DealAmount) : 0
            };

            // Статистика по агентам
            report.AgentStatistics = deals
                .GroupBy(d => d.AgentId)
                .Select(g => new AgentStatisticsDto
                {
                    AgentId = g.Key,
                    AgentName = g.First().Agent?.Username ?? "Неизвестно",
                    TotalDeals = g.Count(),
                    CompletedDeals = g.Count(d => d.Status == "Завершено"),
                    TotalRevenue = g.Where(d => d.Status == "Завершено").Sum(d => d.DealAmount)
                })
                .ToList();

            // Статистика по месяцам
            report.MonthlyStatistics = deals
                .GroupBy(d => new { d.DealDate.Year, d.DealDate.Month })
                .Select(g => new MonthlyStatisticsDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    DealCount = g.Count(),
                    TotalRevenue = g.Sum(d => d.DealAmount),
                    AverageDealAmount = g.Average(d => d.DealAmount)
                })
                .OrderBy(s => s.Year)
                .ThenBy(s => s.Month)
                .ToList();

            // Топ объектов недвижимости
            report.TopProperties = deals
                .GroupBy(d => d.PropertyId)
                .Select(g => new PropertyStatisticsDto
                {
                    PropertyId = g.Key,
                    PropertyTitle = g.First().Property?.Title ?? "Неизвестно",
                    DealCount = g.Count(),
                    TotalRevenue = g.Sum(d => d.DealAmount)
                })
                .OrderByDescending(p => p.TotalRevenue)
                .Take(10)
                .ToList();

            return Ok(report);
        }

        [HttpGet("property-analysis")]
        public async Task<ActionResult<PropertyAnalysisReportDto>> GetPropertyAnalysisReport()
        {
            var properties = await _propertyRepository.GetAllAsync();
            var deals = await _dealRepository.GetAllAsync();

            var completedDeals = deals.Where(d => d.Status == "Завершено").ToList();

            var report = new PropertyAnalysisReportDto
            {
                GeneratedDate = DateTime.UtcNow,
                TotalProperties = properties.Count(),
                AvailableProperties = properties.Count(p => p.IsAvailable),
                SoldProperties = properties.Count(p => !p.IsAvailable),
                TotalPropertyValue = properties.Sum(p => p.Price),
                AveragePropertyPrice = properties.Any() ? properties.Average(p => p.Price) : 0
            };

            // Анализ по типам недвижимости
            report.PropertyTypeAnalysis = properties
                .GroupBy(p => p.Type)
                .Select(g => new PropertyTypeAnalysisDto
                {
                    PropertyType = g.Key,
                    Count = g.Count(),
                    AveragePrice = g.Average(p => p.Price),
                    AverageArea = g.Average(p => p.Area),
                    SoldCount = g.Count(p => !p.IsAvailable),
                    SoldPercentage = g.Count() > 0 ? (double)g.Count(p => !p.IsAvailable) / g.Count() * 100 : 0  // decimal → double
                })
                .ToList();

            // Распределение по комнатам
            report.RoomDistribution = properties
                .GroupBy(p => p.Rooms)
                .Select(g => new RoomDistributionDto
                {
                    Rooms = g.Key,
                    Count = g.Count(),
                    Percentage = properties.Count() > 0 ? (double)g.Count() / properties.Count() * 100 : 0  // decimal → double
                })
                .OrderBy(r => r.Rooms)
                .ToList();

            // Статистика цен
            if (properties.Any())
            {
                var prices = properties.Select(p => p.Price).ToList();
                report.MinPrice = prices.Min();
                report.MaxPrice = prices.Max();
                report.MedianPrice = CalculateMedian(prices);

                // Ценовые сегменты
                report.PriceSegments = new List<PriceSegmentDto>
                {
                    new PriceSegmentDto { Range = "До 3 млн", Count = prices.Count(p => p <= 3000000) },
                    new PriceSegmentDto { Range = "3-6 млн", Count = prices.Count(p => p > 3000000 && p <= 6000000) },
                    new PriceSegmentDto { Range = "6-10 млн", Count = prices.Count(p => p > 6000000 && p <= 10000000) },
                    new PriceSegmentDto { Range = "Свыше 10 млн", Count = prices.Count(p => p > 10000000) }
                };
            }

            // Статистика сделок по объектам
            var propertyDeals = completedDeals
                .GroupBy(d => d.PropertyId)
                .ToDictionary(g => g.Key, g => g.ToList());

            report.PropertiesWithDeals = propertyDeals.Count;
            report.AverageDealAmountPerProperty = propertyDeals.Any()
                ? propertyDeals.Average(kvp => kvp.Value.Sum(d => d.DealAmount))
                : 0;

            return Ok(report);
        }

        [HttpGet("agent-performance")]
        public async Task<ActionResult<List<AgentPerformanceDto>>> GetAgentPerformanceReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (!startDate.HasValue)
                startDate = DateTime.Now.AddMonths(-3);
            if (!endDate.HasValue)
                endDate = DateTime.Now;

            var deals = await _dealRepository.GetDealsByDateRangeAsync(startDate.Value, endDate.Value);
            var agents = await _userRepository.FindAsync(u => u.Role == "User");

            var performanceReport = new List<AgentPerformanceDto>();

            foreach (var agent in agents)
            {
                var agentDeals = deals.Where(d => d.AgentId == agent.Id).ToList();

                // Фильтруем если не админ
                if (userRole != "Admin" && agent.Id != userId)
                    continue;

                var performance = new AgentPerformanceDto
                {
                    AgentId = agent.Id,
                    AgentName = agent.Username,
                    TotalDeals = agentDeals.Count,
                    CompletedDeals = agentDeals.Count(d => d.Status == "Завершено"),
                    PendingDeals = agentDeals.Count(d => d.Status == "В ожидании"),
                    CancelledDeals = agentDeals.Count(d => d.Status == "Отменено"),
                    TotalRevenue = agentDeals.Where(d => d.Status == "Завершено").Sum(d => d.DealAmount),
                    AverageDealAmount = agentDeals.Any() ? agentDeals.Average(d => d.DealAmount) : 0,
                    SuccessRate = agentDeals.Any() ? (double)agentDeals.Count(d => d.Status == "Завершено") / agentDeals.Count * 100 : 0,  // decimal → double
                    AverageDealTime = CalculateAverageDealTime(agentDeals)
                };

                // Клиенты агента - НУЖНО СОЗДАТЬ метод GetClientsByAgentAsync
                var clients = await _clientRepository.GetClientsByAgentAsync(agent.Id);
                performance.TotalClients = clients.Count();
                performance.ActiveClients = clients.Count(c =>
                    c.Deals != null && c.Deals.Any(d => d.DealDate >= startDate.Value.AddMonths(-1)));

                performanceReport.Add(performance);
            }

            return Ok(performanceReport.OrderByDescending(p => p.TotalRevenue));
        }

        [HttpGet("client-analysis")]
        public async Task<ActionResult<ClientAnalysisReportDto>> GetClientAnalysisReport()
        {
            var clients = await _clientRepository.GetAllAsync();
            var deals = await _dealRepository.GetAllAsync();

            var report = new ClientAnalysisReportDto
            {
                GeneratedDate = DateTime.UtcNow,
                TotalClients = clients.Count(),
                ClientsWithDeals = clients.Count(c => c.Deals != null && c.Deals.Any()),
                AverageBudget = clients.Where(c => c.Budget.HasValue).Average(c => c.Budget.Value),
                TotalPotentialValue = clients.Where(c => c.Budget.HasValue).Sum(c => c.Budget.Value)
            };

            // Распределение клиентов по бюджету
            report.BudgetDistribution = new List<BudgetSegmentDto>
            {
                new BudgetSegmentDto { Range = "До 2 млн", Count = clients.Count(c => c.Budget <= 2000000) },
                new BudgetSegmentDto { Range = "2-5 млн", Count = clients.Count(c => c.Budget > 2000000 && c.Budget <= 5000000) },
                new BudgetSegmentDto { Range = "5-10 млн", Count = clients.Count(c => c.Budget > 5000000 && c.Budget <= 10000000) },
                new BudgetSegmentDto { Range = "Свыше 10 млн", Count = clients.Count(c => c.Budget > 10000000) }
            };

            // Топ клиентов по количеству сделок
            report.TopClients = clients
                .Where(c => c.Deals != null && c.Deals.Any())
                .Select(c => new ClientStatisticsDto
                {
                    ClientId = c.Id,
                    ClientName = $"{c.FirstName} {c.LastName}",
                    DealCount = c.Deals.Count,
                    TotalSpent = c.Deals.Sum(d => d.DealAmount),
                    LastDealDate = c.Deals.Max(d => d.DealDate)
                })
                .OrderByDescending(c => c.TotalSpent)
                .Take(10)
                .ToList();

            // Анализ требований клиентов
            var requirements = clients
                .Where(c => !string.IsNullOrEmpty(c.Requirements))
                .SelectMany(c => c.Requirements.Split(new[] { ',', ';', '.' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(r => r.Trim().ToLower())
                .Where(r => r.Length > 3)
                .GroupBy(r => r)
                .Select(g => new RequirementAnalysisDto
                {
                    Requirement = g.Key,
                    Count = g.Count(),
                    Percentage = (double)g.Count() / clients.Count() * 100  // decimal → double
                })
                .OrderByDescending(r => r.Count)
                .Take(20)
                .ToList();

            report.TopRequirements = requirements;

            return Ok(report);
        }

        [HttpGet("financial")]
        public async Task<ActionResult<FinancialReportDto>> GetFinancialReport(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            if (!startDate.HasValue)
                startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            if (!endDate.HasValue)
                endDate = DateTime.Now;

            var deals = await _dealRepository.GetDealsByDateRangeAsync(startDate.Value, endDate.Value);
            var completedDeals = deals.Where(d => d.Status == "Завершено").ToList();

            // УБЕРИТЕ 'm' суффикс для decimal, используйте 0.03 как double
            var commissionRate = 0.03; // 3% комиссия

            var report = new FinancialReportDto
            {
                StartDate = startDate.Value,
                EndDate = endDate.Value,
                GeneratedDate = DateTime.UtcNow,
                TotalRevenue = completedDeals.Sum(d => d.DealAmount),
                TotalDeals = completedDeals.Count,
                AverageCommission = completedDeals.Any() ? completedDeals.Average(d => d.DealAmount * commissionRate) : 0,
                EstimatedCommission = completedDeals.Sum(d => d.DealAmount * commissionRate)
            };

            // Ежемесячная выручка
            report.MonthlyRevenue = completedDeals
                .GroupBy(d => new { d.DealDate.Year, d.DealDate.Month })
                .Select(g => new MonthlyRevenueDto
                {
                    Year = g.Key.Year,
                    Month = g.Key.Month,
                    Revenue = g.Sum(d => d.DealAmount),
                    Commission = g.Sum(d => d.DealAmount * commissionRate),
                    DealCount = g.Count()
                })
                .OrderBy(m => m.Year)
                .ThenBy(m => m.Month)
                .ToList();

            // Прогноз на следующий месяц
            var lastMonthRevenue = report.MonthlyRevenue.LastOrDefault();
            if (lastMonthRevenue != null)
            {
                report.NextMonthForecast = new FinancialForecastDto
                {
                    ForecastedRevenue = lastMonthRevenue.Revenue * 1.1, // +10% (без 'm')
                    ForecastedCommission = lastMonthRevenue.Commission * 1.1, // без 'm'
                    ConfidenceLevel = 0.85 // без 'm'
                };
            }

            return Ok(report);
        }

        // ИЗМЕНИТЬ параметр с List<decimal> на List<double>
        private double CalculateMedian(List<double> numbers)  // List<decimal> → List<double>
        {
            if (!numbers.Any()) return 0;

            var sorted = numbers.OrderBy(n => n).ToList();
            int count = sorted.Count;
            int midpoint = count / 2;

            if (count % 2 == 0)
            {
                return (sorted[midpoint - 1] + sorted[midpoint]) / 2;
            }
            else
            {
                return sorted[midpoint];
            }
        }

        private TimeSpan CalculateAverageDealTime(List<Deal> deals)
        {
            if (!deals.Any()) return TimeSpan.Zero;

            var completedDeals = deals.Where(d => d.Status == "Завершено").ToList();
            if (!completedDeals.Any()) return TimeSpan.Zero;

            var totalDays = completedDeals.Sum(d => (d.DealDate - d.CreatedAt).Days);
            return TimeSpan.FromDays(totalDays / completedDeals.Count);
        }
    }
}