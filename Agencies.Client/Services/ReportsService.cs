// ReportService.cs в клиентском проекте (Agencies.Client.Services)
using Agencies.Core.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Agencies.Client.Services
{
    public class ReportService
    {
        private IEnumerable<DealDto> _deals;
        private IEnumerable<PropertyDto> _properties;
        private IEnumerable<ClientDto> _clients;

        // Конструктор принимает коллекции данных
        public ReportService(
            IEnumerable<DealDto> deals,
            IEnumerable<PropertyDto> properties,
            IEnumerable<ClientDto> clients)
        {
            _deals = deals;
            _properties = properties;
            _clients = clients;
        }

        // Альтернативный конструктор для совместимости
        public ReportService() : this(new List<DealDto>(), new List<PropertyDto>(), new List<ClientDto>())
        {
        }

        // Метод для обновления данных
        public void UpdateData(
            IEnumerable<DealDto> deals,
            IEnumerable<PropertyDto> properties,
            IEnumerable<ClientDto> clients)
        {
            _deals = deals;
            _properties = properties;
            _clients = clients;
        }

        public async Task<SalesReportDto> GenerateSalesReportAsync(DateTime startDate, DateTime endDate)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Фильтруем сделки по дате
                    var filteredDeals = _deals
                        .Where(d => d.DealDate >= startDate && d.DealDate <= endDate)
                        .ToList();

                    var report = new SalesReportDto
                    {
                        StartDate = startDate,
                        EndDate = endDate,
                        GeneratedDate = DateTime.Now,
                        TotalDeals = filteredDeals.Count,
                        CompletedDeals = filteredDeals.Count(d => d.Status == "Завершено"),
                        PendingDeals = filteredDeals.Count(d => d.Status == "В ожидании"),
                        CancelledDeals = filteredDeals.Count(d => d.Status == "Отменено"),
                        TotalRevenue = filteredDeals
                            .Where(d => d.Status == "Завершено")
                            .Sum(d => d.DealAmount),
                        AverageDealAmount = filteredDeals.Any() ?
                            (double)filteredDeals.Average(d => d.DealAmount) : 0
                    };

                    // Статистика по агентам
                    report.AgentStatistics = filteredDeals
                        .GroupBy(d => d.AgentId)
                        .Select(g =>
                        {
                            var agentDeals = g.ToList();
                            var firstDeal = agentDeals.FirstOrDefault();
                            return new AgentStatisticsDto
                            {
                                AgentId = g.Key,
                                AgentName = firstDeal != null ? firstDeal.AgentName : "Неизвестный агент",
                                TotalDeals = agentDeals.Count,
                                CompletedDeals = agentDeals.Count(d => d.Status == "Завершено"),
                                TotalRevenue = agentDeals
                                    .Where(d => d.Status == "Завершено")
                                    .Sum(d => d.DealAmount)
                            };
                        })
                        .OrderByDescending(a => a.TotalRevenue)
                        .ToList();

                    // Статистика по месяцам
                    report.MonthlyStatistics = filteredDeals
                        .GroupBy(d => new { d.DealDate.Year, d.DealDate.Month })
                        .Select(g =>
                        {
                            var monthDeals = g.ToList();
                            var completedDeals = monthDeals.Where(d => d.Status == "Завершено").ToList();

                            return new MonthlyStatisticsDto
                            {
                                Year = g.Key.Year,
                                Month = g.Key.Month,
                                DealCount = monthDeals.Count,
                                TotalRevenue = completedDeals.Sum(d => d.DealAmount),
                                AverageDealAmount = completedDeals.Any() ?
                                    (double)completedDeals.Average(d => d.DealAmount) : 0
                            };
                        })
                        .OrderBy(m => m.Year)
                        .ThenBy(m => m.Month)
                        .ToList();

                    // Топ объектов
                    report.TopProperties = filteredDeals
                        .Where(d => d.PropertyId > 0 && d.Status == "Завершено")
                        .GroupBy(d => d.PropertyId)
                        .Select(g =>
                        {
                            var propertyDeals = g.ToList();
                            var property = _properties.FirstOrDefault(p => p.Id == g.Key);

                            return new PropertyStatisticsDto
                            {
                                PropertyId = g.Key,
                                PropertyTitle = property?.Title ?? "Неизвестный объект",
                                DealCount = propertyDeals.Count,
                                TotalRevenue = propertyDeals.Sum(d => d.DealAmount)
                            };
                        })
                        .OrderByDescending(p => p.TotalRevenue)
                        .Take(10)
                        .ToList();

                    return report;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка генерации отчета: {ex.Message}", ex);
                }
            });
        }

        public async Task<PropertyAnalysisReportDto> GeneratePropertyAnalysisReportAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var properties = _properties.ToList();
                    var deals = _deals.ToList();

                    var report = new PropertyAnalysisReportDto
                    {
                        GeneratedDate = DateTime.Now,
                        TotalProperties = properties.Count,
                        AvailableProperties = properties.Count(p => p.IsAvailable),
                        SoldProperties = properties.Count(p => !p.IsAvailable),
                        TotalPropertyValue = properties.Sum(p => p.Price),
                        AveragePropertyPrice = properties.Any() ?
                            (double)properties.Average(p => p.Price) : 0,
                        MinPrice = properties.Any() ?
                            (double)properties.Min(p => p.Price) : 0,
                        MaxPrice = properties.Any() ?
                            (double)properties.Max(p => p.Price) : 0,
                        PropertiesWithDeals = properties
                            .Count(p => deals.Any(d => d.PropertyId == p.Id)),
                        AverageDealAmountPerProperty = deals.Any() ?
                            (double)deals.Average(d => d.DealAmount) : 0
                    };

                    // Расчет медианной цены
                    if (properties.Any())
                    {
                        var prices = properties.Select(p => p.Price).OrderBy(p => p).ToList();
                        int count = prices.Count;
                        if (count % 2 == 0)
                        {
                            report.MedianPrice = (prices[count / 2 - 1] + prices[count / 2]) / 2;
                        }
                        else
                        {
                            report.MedianPrice = prices[count / 2];
                        }
                    }

                    // Анализ по типам недвижимости
                    report.PropertyTypeAnalysis = properties
                        .GroupBy(p => p.Type)
                        .Select(g =>
                        {
                            var typeProperties = g.ToList();
                            var typeDeals = deals.Where(d =>
                                typeProperties.Any(p => p.Id == d.PropertyId && d.Status == "Завершено")).ToList();

                            return new PropertyTypeAnalysisDto
                            {
                                PropertyType = g.Key,
                                Count = typeProperties.Count,
                                AveragePrice = typeProperties.Any() ?
                                    (double)typeProperties.Average(p => p.Price) : 0,
                                AverageArea = typeProperties.Any() ?
                                    (double)typeProperties.Average(p => p.Area) : 0,
                                SoldCount = typeDeals.Count,
                                SoldPercentage = typeProperties.Count > 0 ?
                                    (double)typeDeals.Count / typeProperties.Count * 100 : 0
                            };
                        })
                        .OrderByDescending(t => t.Count)
                        .ToList();

                    // Распределение по количеству комнат
                    // Если Rooms - это int (не nullable), то просто группируем
                    // Если Rooms - это int?, то нужно обрабатывать null значения
                    report.RoomDistribution = properties
                        .GroupBy(p => p.Rooms) // Просто группируем по значению
                        .Select(g => new RoomDistributionDto
                        {
                            Rooms = g.Key, // Для int? это будет int?, для int - int
                            Count = g.Count(),
                            Percentage = properties.Count > 0 ?
                                (double)g.Count() / properties.Count * 100 : 0
                        })
                        .OrderBy(r => r.Rooms)
                        .ToList();

                    // Ценовые сегменты
                    report.PriceSegments = new List<PriceSegmentDto>
                    {
                        new PriceSegmentDto { Range = "До 1 млн", Count = properties.Count(p => p.Price <= 1000000) },
                        new PriceSegmentDto { Range = "1-3 млн", Count = properties.Count(p => p.Price > 1000000 && p.Price <= 3000000) },
                        new PriceSegmentDto { Range = "3-5 млн", Count = properties.Count(p => p.Price > 3000000 && p.Price <= 5000000) },
                        new PriceSegmentDto { Range = "5-10 млн", Count = properties.Count(p => p.Price > 5000000 && p.Price <= 10000000) },
                        new PriceSegmentDto { Range = "Более 10 млн", Count = properties.Count(p => p.Price > 10000000) }
                    };

                    return report;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка генерации анализа объектов: {ex.Message}", ex);
                }
            });
        }

        // В методе GenerateClientAnalysisReportAsync:
        public async Task<ClientAnalysisReportDto> GenerateClientAnalysisReportAsync()
        {
            return await Task.Run(() =>
            {
                try
                {
                    var clients = _clients.ToList();
                    var deals = _deals.ToList();

                    // Фильтруем клиентов с бюджетом (не null)
                    var clientsWithBudget = clients.Where(c => c.Budget.HasValue).ToList();

                    var report = new ClientAnalysisReportDto
                    {
                        GeneratedDate = DateTime.Now,
                        TotalClients = clients.Count,
                        ClientsWithDeals = clients
                            .Count(c => deals.Any(d => d.ClientId == c.Id)),
                        AverageBudget = clientsWithBudget.Any() ?
                            (double)clientsWithBudget.Average(c => c.Budget.Value) : 0,
                        TotalPotentialValue = clientsWithBudget.Sum(c => c.Budget ?? 0)
                    };

                    // Распределение по бюджету (только клиенты с указанным бюджетом)
                    var budgetedClients = clients.Where(c => c.Budget.HasValue).ToList();
                    report.BudgetDistribution = new List<BudgetSegmentDto>
            {
                new BudgetSegmentDto { Range = "До 500 тыс.",
                    Count = budgetedClients.Count(c => c.Budget <= 500000) },
                new BudgetSegmentDto { Range = "500 тыс. - 1 млн",
                    Count = budgetedClients.Count(c => c.Budget > 500000 && c.Budget <= 1000000) },
                new BudgetSegmentDto { Range = "1-3 млн",
                    Count = budgetedClients.Count(c => c.Budget > 1000000 && c.Budget <= 3000000) },
                new BudgetSegmentDto { Range = "3-5 млн",
                    Count = budgetedClients.Count(c => c.Budget > 3000000 && c.Budget <= 5000000) },
                new BudgetSegmentDto { Range = "Более 5 млн",
                    Count = budgetedClients.Count(c => c.Budget > 5000000) }
            };

                    // Топ клиентов по потраченным суммам
                    report.TopClients = deals
                        .Where(d => d.Status == "Завершено")
                        .GroupBy(d => d.ClientId)
                        .Select(g =>
                        {
                            var clientDeals = g.ToList();
                            var client = clients.FirstOrDefault(c => c.Id == g.Key);

                            return new ClientStatisticsDto
                            {
                                ClientId = g.Key,
                                ClientName = client != null ?
                                    $"{client.FirstName} {client.LastName}" : "Неизвестный клиент",
                                DealCount = clientDeals.Count,
                                TotalSpent = clientDeals.Sum(d => d.DealAmount),
                                LastDealDate = clientDeals.Max(d => d.DealDate)
                            };
                        })
                        .OrderByDescending(c => c.TotalSpent)
                        .Take(10)
                        .ToList();

                    // Анализ требований (топ 10)
                    var allRequirements = clients
                        .Where(c => !string.IsNullOrEmpty(c.Requirements))
                        .SelectMany(c => c.Requirements.Split(new[] { ',', ';', '.' }, StringSplitOptions.RemoveEmptyEntries))
                        .Select(r => r.Trim().ToLower())
                        .Where(r => !string.IsNullOrEmpty(r))
                        .ToList();

                    report.TopRequirements = allRequirements
                        .GroupBy(r => r)
                        .Select(g => new RequirementAnalysisDto
                        {
                            Requirement = g.Key,
                            Count = g.Count(),
                            Percentage = allRequirements.Count > 0 ?
                                (double)g.Count() / allRequirements.Count * 100 : 0
                        })
                        .OrderByDescending(r => r.Count)
                        .Take(10)
                        .ToList();

                    return report;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка генерации анализа клиентов: {ex.Message}", ex);
                }
            });
        }

        public async Task<FinancialReportDto> GenerateFinancialReportAsync(DateTime startDate, DateTime endDate)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var filteredDeals = _deals
                        .Where(d => d.DealDate >= startDate && d.DealDate <= endDate && d.Status == "Завершено")
                        .ToList();

                    var report = new FinancialReportDto
                    {
                        StartDate = startDate,
                        EndDate = endDate,
                        GeneratedDate = DateTime.Now,
                        TotalRevenue = filteredDeals.Sum(d => d.DealAmount),
                        TotalDeals = filteredDeals.Count,
                        AverageCommission = filteredDeals.Any() ?
                            (double)filteredDeals.Average(d => d.DealAmount) * 0.03 : 0,
                        EstimatedCommission = filteredDeals.Sum(d => d.DealAmount) * 0.03
                    };

                    // Выручка по месяцам
                    report.MonthlyRevenue = filteredDeals
                        .GroupBy(d => new { d.DealDate.Year, d.DealDate.Month })
                        .Select(g =>
                        {
                            var monthDeals = g.ToList();
                            return new MonthlyRevenueDto
                            {
                                Year = g.Key.Year,
                                Month = g.Key.Month,
                                Revenue = monthDeals.Sum(d => d.DealAmount),
                                Commission = monthDeals.Sum(d => d.DealAmount) * 0.03,
                                DealCount = monthDeals.Count
                            };
                        })
                        .OrderBy(m => m.Year)
                        .ThenBy(m => m.Month)
                        .ToList();

                    // Прогноз на следующий месяц (простая реализация)
                    var last3Months = report.MonthlyRevenue
                        .OrderByDescending(m => m.Year)
                        .ThenByDescending(m => m.Month)
                        .Take(3)
                        .ToList();

                    if (last3Months.Any())
                    {
                        report.NextMonthForecast = new FinancialForecastDto
                        {
                            ForecastedRevenue = last3Months.Average(m => m.Revenue),
                            ForecastedCommission = last3Months.Average(m => m.Commission),
                            ConfidenceLevel = last3Months.Count >= 3 ? 0.85 : 0.65
                        };
                    }

                    return report;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка генерации финансового отчета: {ex.Message}", ex);
                }
            });
        }

        public async Task<List<AgentPerformanceDto>> GenerateAgentPerformanceReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    var deals = _deals.ToList();
                    var clients = _clients.ToList();

                    var filteredDeals = deals
                        .Where(d =>
                            (!startDate.HasValue || d.DealDate >= startDate.Value) &&
                            (!endDate.HasValue || d.DealDate <= endDate.Value))
                        .ToList();

                    var agentReports = filteredDeals
                        .GroupBy(d => d.AgentId)
                        .Select(g =>
                        {
                            var agentDeals = g.ToList();
                            var agentClients = clients
                                .Where(c => agentDeals.Any(d => d.ClientId == c.Id))
                                .ToList();

                            var completedDeals = agentDeals.Where(d => d.Status == "Завершено").ToList();
                            var firstDeal = agentDeals.FirstOrDefault();

                            // Расчет среднего времени сделки
                            TimeSpan averageTime = TimeSpan.Zero;
                            if (completedDeals.Any())
                            {
                                var totalDays = completedDeals.Sum(d =>
                                    (d.DealDate - d.CreatedAt).TotalDays);
                                averageTime = TimeSpan.FromDays(totalDays / completedDeals.Count);
                            }

                            return new AgentPerformanceDto
                            {
                                AgentId = g.Key,
                                AgentName = firstDeal != null ? firstDeal.AgentName : "Неизвестный агент",
                                TotalDeals = agentDeals.Count,
                                CompletedDeals = completedDeals.Count,
                                PendingDeals = agentDeals.Count(d => d.Status == "В ожидании"),
                                CancelledDeals = agentDeals.Count(d => d.Status == "Отменено"),
                                TotalRevenue = completedDeals.Sum(d => d.DealAmount),
                                AverageDealAmount = completedDeals.Any() ?
                                    (double)completedDeals.Average(d => d.DealAmount) : 0,
                                SuccessRate = agentDeals.Any() ?
                                    (double)completedDeals.Count / agentDeals.Count * 100 : 0,
                                AverageDealTime = averageTime,
                                TotalClients = agentClients.Count,
                                ActiveClients = agentClients
                                    .Count(c => deals.Any(d =>
                                        d.ClientId == c.Id &&
                                        d.DealDate >= DateTime.Now.AddMonths(-3)))
                            };
                        })
                        .OrderByDescending(a => a.SuccessRate)
                        .ThenByDescending(a => a.TotalRevenue)
                        .ToList();

                    return agentReports;
                }
                catch (Exception ex)
                {
                    throw new Exception($"Ошибка генерации отчета по агентам: {ex.Message}", ex);
                }
            });
        }
    }
}
