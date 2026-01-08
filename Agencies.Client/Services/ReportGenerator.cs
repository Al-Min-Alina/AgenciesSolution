using Agencies.Core.DTO;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Agencies.Client.Services
{
    public class ReportGenerator
    {
        private readonly ApiService _apiService;

        public ReportGenerator(ApiService apiService)
        {
            _apiService = apiService;
        }

        public async Task<SalesReport> GenerateSalesReportAsync(DateTime startDate, DateTime endDate,
            CancellationToken cancellationToken = default)
        {
            var report = new SalesReport
            {
                StartDate = startDate,
                EndDate = endDate,
                GeneratedDate = DateTime.Now
            };

            try
            {
                // Загружаем сделки параллельно с фильтрацией по дате
                var deals = await Task.Run(async () =>
                {
                    var allDeals = await _apiService.GetDealsAsync();
                    return allDeals.Where(d => d.DealDate >= startDate && d.DealDate <= endDate).ToList();
                }, cancellationToken);

                cancellationToken.ThrowIfCancellationRequested();

                // Используем Parallel.ForEach для параллельной обработки
                var lockObject = new object();
                var agentStats = new ConcurrentDictionary<int, AgentStatistics>();

                Parallel.ForEach(deals, deal =>
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    lock (lockObject)
                    {
                        report.TotalDeals++;
                        report.TotalRevenue += deal.DealAmount;

                        if (deal.Status == "Завершено")
                        {
                            report.CompletedDeals++;
                        }
                        else if (deal.Status == "Отменено")
                        {
                            report.PendingDeals++;
                        }

                        // Собираем статистику по агентам
                        if (!agentStats.ContainsKey(deal.AgentId))
                        {
                            agentStats[deal.AgentId] = new AgentStatistics
                            {
                                AgentId = deal.AgentId,
                                AgentName = deal.AgentName
                            };
                        }

                        var stats = agentStats[deal.AgentId];
                        stats.TotalDeals++;
                        stats.TotalRevenue += deal.DealAmount;
                        if (deal.Status == "Завершено") stats.CompletedDeals++;
                        agentStats[deal.AgentId] = stats;
                    }
                });

                report.AgentStatistics = agentStats.Values.ToList();
                report.AverageDealAmount = report.TotalDeals > 0 ?
                    report.TotalRevenue / report.TotalDeals : 0;

                // Группируем сделки по месяцам
                report.MonthlyStats = deals
                    .GroupBy(d => new { d.DealDate.Year, d.DealDate.Month })
                    .Select(g =>
                    {
                        var dealList = g.ToList();
                        var count = dealList.Count;
                        var totalRevenue = dealList.Sum(d => d.DealAmount);

                        return new MonthlyStatistics
                        {
                            Year = g.Key.Year,
                            Month = g.Key.Month,
                            DealCount = count,
                            TotalRevenue = totalRevenue,
                            AverageDealAmount = count > 0 ? totalRevenue / count : 0
                        };
                    })
                    .OrderBy(s => s.Year)
                    .ThenBy(s => s.Month)
                    .ToList();

                return report;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ReportGenerationException("Ошибка генерации отчета", ex);
            }
        }

        public async Task<PropertyAnalysisReport> GeneratePropertyAnalysisAsync(
            CancellationToken cancellationToken = default)
        {
            var report = new PropertyAnalysisReport
            {
                GeneratedDate = DateTime.Now
            };

            try
            {
                // Загружаем данные параллельно
                var propertiesTask = Task.Run(() => _apiService.GetPropertiesAsync(), cancellationToken);
                var dealsTask = Task.Run(() => _apiService.GetDealsAsync(), cancellationToken);

                await Task.WhenAll(propertiesTask, dealsTask);

                var properties = await propertiesTask;
                var deals = await dealsTask;

                cancellationToken.ThrowIfCancellationRequested();

                // Анализируем свойства
                report.TotalProperties = properties.Count;
                report.AvailableProperties = properties.Count(p => p.IsAvailable);
                report.SoldProperties = properties.Count(p => !p.IsAvailable);

                // Анализ по типам недвижимости
                report.PropertyTypeAnalysis = properties
                    .GroupBy(p => p.Type)
                    .Select(g =>
                    {
                        var propertyList = g.ToList();
                        var count = propertyList.Count;
                        var totalPrice = propertyList.Sum(p => p.Price);
                        var totalArea = propertyList.Sum(p => p.Area);

                        return new PropertyTypeAnalysis
                        {
                            PropertyType = g.Key,
                            Count = count,
                            AveragePrice = count > 0 ? totalPrice / count : 0,
                            AverageArea = count > 0 ? totalArea / count : 0  
                        };
                    })
                    .ToList();

                // Анализ цен
                if (properties.Any())
                {
                    report.MinPrice = properties.Min(p => p.Price);
                    report.MaxPrice = properties.Max(p => p.Price);

                    var propertiesList = properties.ToList();
                    var count = propertiesList.Count;
                    var totalPrice = propertiesList.Sum(p => p.Price);
                    report.AveragePrice = count > 0 ? totalPrice / count : 0;

                    report.MedianPrice = CalculateMedian(properties.Select(p => p.Price).ToList());
                }

                // Анализ сделок по свойствам
                var propertyDeals = deals
                    .Where(d => d.Status == "Завершено")
                    .GroupBy(d => d.PropertyId)
                    .ToDictionary(g => g.Key, g => g.ToList());

                report.PropertiesWithDeals = propertyDeals.Count;

                if (propertyDeals.Any())
                {
                    var totalDealsCount = propertyDeals.Sum(kvp => kvp.Value.Count);
                    report.AverageDealsPerProperty = (double)totalDealsCount / propertyDeals.Count;  // decimal → double
                }
                else
                {
                    report.AverageDealsPerProperty = 0;
                }

                return report;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new ReportGenerationException("Ошибка генерации анализа свойств", ex);
            }
        }

        private double CalculateMedian(List<double> numbers)  
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
    }

    public class SalesReport
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public DateTime GeneratedDate { get; set; }
        public int TotalDeals { get; set; }
        public int CompletedDeals { get; set; }
        public int PendingDeals { get; set; }
        public double TotalRevenue { get; set; }              
        public double AverageDealAmount { get; set; }        
        public List<AgentStatistics> AgentStatistics { get; set; }
        public List<MonthlyStatistics> MonthlyStats { get; set; }
    }

    public class AgentStatistics
    {
        public int AgentId { get; set; }
        public string AgentName { get; set; }
        public int TotalDeals { get; set; }
        public int CompletedDeals { get; set; }
        public double TotalRevenue { get; set; }              
        public double SuccessRate => TotalDeals > 0 ?         
            (double)CompletedDeals / TotalDeals * 100 : 0;    
    }

    public class MonthlyStatistics
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName => new DateTime(Year, Month, 1).ToString("MMMM yyyy");
        public int DealCount { get; set; }
        public double TotalRevenue { get; set; }              
        public double AverageDealAmount { get; set; }         
    }

    public class PropertyAnalysisReport
    {
        public DateTime GeneratedDate { get; set; }
        public int TotalProperties { get; set; }
        public int AvailableProperties { get; set; }
        public int SoldProperties { get; set; }
        public double MinPrice { get; set; }                  
        public double MaxPrice { get; set; }                  
        public double AveragePrice { get; set; }              
        public double MedianPrice { get; set; }               
        public List<PropertyTypeAnalysis> PropertyTypeAnalysis { get; set; }
        public int PropertiesWithDeals { get; set; }
        public double AverageDealsPerProperty { get; set; }   
    }

    public class PropertyTypeAnalysis
    {
        public string PropertyType { get; set; }
        public int Count { get; set; }
        public double AveragePrice { get; set; }              
        public double AverageArea { get; set; }               
    }

    public class ReportGenerationException : Exception
    {
        public ReportGenerationException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}