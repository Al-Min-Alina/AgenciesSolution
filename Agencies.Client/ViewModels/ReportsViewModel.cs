using Agencies.Client.Services;
using Agencies.Core.DTO;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace Agencies.Client.ViewModels
{
    public class ReportsViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly ReportGenerator _reportGenerator;

        private SalesReportDto _salesReport;
        public SalesReportDto SalesReport
        {
            get => _salesReport;
            set => SetProperty(ref _salesReport, value);
        }

        private PropertyAnalysisReportDto _propertyAnalysis;
        public PropertyAnalysisReportDto PropertyAnalysis
        {
            get => _propertyAnalysis;
            set => SetProperty(ref _propertyAnalysis, value);
        }

        private ObservableCollection<AgentPerformanceDto> _agentPerformance;
        public ObservableCollection<AgentPerformanceDto> AgentPerformance
        {
            get => _agentPerformance;
            set => SetProperty(ref _agentPerformance, value);
        }

        private DateTime _startDate = DateTime.Now.AddMonths(-6);
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }

        private DateTime _endDate = DateTime.Now;
        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _progressText;
        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        // Charts data
        public SeriesCollection MonthlyRevenueSeries { get; set; }
        public SeriesCollection DealStatusSeries { get; set; }
        public SeriesCollection AgentPerformanceSeries { get; set; }
        public string[] MonthLabels { get; set; }
        public string[] AgentLabels { get; set; }

        public Func<double, string> CurrencyFormatter { get; set; }  // double вместо decimal

        public ReportsViewModel(ApiService apiService)
        {
            _apiService = apiService;
            _reportGenerator = new ReportGenerator(apiService);

            InitializeCharts();
        }

        private void InitializeCharts()
        {
            CurrencyFormatter = value => value.ToString("C0");  // value уже double

            MonthlyRevenueSeries = new SeriesCollection();
            DealStatusSeries = new SeriesCollection();
            AgentPerformanceSeries = new SeriesCollection();
        }

        public async Task LoadSalesReportAsync()
        {
            try
            {
                IsLoading = true;
                ProgressText = "Загрузка отчета по продажам...";

                SalesReport = await _apiService.GetSalesReportAsync(StartDate, EndDate);

                // Update charts
                UpdateCharts();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки отчета: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                ProgressText = string.Empty;
            }
        }

        public async Task LoadPropertyAnalysisAsync()
        {
            try
            {
                IsLoading = true;
                ProgressText = "Загрузка анализа объектов...";

                PropertyAnalysis = await _apiService.GetPropertyAnalysisReportAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки анализа: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                ProgressText = string.Empty;
            }
        }

        public async Task LoadAgentPerformanceAsync()
        {
            try
            {
                IsLoading = true;
                ProgressText = "Загрузка отчета по агентам...";

                var performance = await _apiService.GetAgentPerformanceReportAsync(StartDate, EndDate);
                AgentPerformance = new ObservableCollection<AgentPerformanceDto>(performance);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки отчета: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                ProgressText = string.Empty;
            }
        }

        public async Task LoadAllReportsAsync()
        {
            var tasks = new[]
            {
                LoadSalesReportAsync(),
                LoadPropertyAnalysisAsync(),
                LoadAgentPerformanceAsync()
            };

            await Task.WhenAll(tasks);
        }

        private void UpdateCharts()
        {
            if (SalesReport == null) return;

            // Monthly Revenue Chart - ИЗМЕНИТЬ ChartValues<decimal> на ChartValues<double>
            MonthlyRevenueSeries.Clear();
            var columnSeries = new ColumnSeries
            {
                Title = "Выручка",
                Values = new ChartValues<double>(SalesReport.MonthlyStatistics.Select(m => m.TotalRevenue)),
                Fill = Brushes.CornflowerBlue
            };
            MonthlyRevenueSeries.Add(columnSeries);

            MonthLabels = SalesReport.MonthlyStatistics.Select(m => m.MonthName).ToArray();

            // Deal Status Pie Chart - ИЗМЕНИТЬ ChartValues<decimal> на ChartValues<double>
            DealStatusSeries.Clear();
            if (SalesReport.TotalDeals > 0)
            {
                // Для целых чисел тоже нужно использовать double
                DealStatusSeries.Add(new PieSeries
                {
                    Title = "Завершено",
                    Values = new ChartValues<double> { (double)SalesReport.CompletedDeals },
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:F0} ({point.Participation:P1})",  // F0 для целых чисел
                    Fill = Brushes.Green
                });

                DealStatusSeries.Add(new PieSeries
                {
                    Title = "В процессе",
                    Values = new ChartValues<double> { (double)SalesReport.PendingDeals },
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:F0} ({point.Participation:P1})",
                    Fill = Brushes.Orange
                });

                DealStatusSeries.Add(new PieSeries
                {
                    Title = "Отменено",
                    Values = new ChartValues<double> { (double)SalesReport.CancelledDeals },
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y:F0} ({point.Participation:P1})",
                    Fill = Brushes.Red
                });
            }

            // Agent Performance Chart - ИЗМЕНИТЬ ChartValues<decimal> на ChartValues<double>
            AgentPerformanceSeries.Clear();
            if (SalesReport.AgentStatistics != null && SalesReport.AgentStatistics.Any())
            {
                var agentSeries = new ColumnSeries
                {
                    Title = "Выручка агентов",
                    Values = new ChartValues<double>(SalesReport.AgentStatistics.Select(a => a.TotalRevenue)),
                    Fill = Brushes.SteelBlue
                };
                AgentPerformanceSeries.Add(agentSeries);

                AgentLabels = SalesReport.AgentStatistics.Select(a => a.AgentName).ToArray();
            }
        }

        public async Task GenerateDetailedReportAsync()
        {
            try
            {
                IsLoading = true;
                ProgressText = "Генерация детального отчета...";

                // Используем ReportGenerator для сложных расчетов
                var salesReport = await _reportGenerator.GenerateSalesReportAsync(StartDate, EndDate);

                // УБЕДИТЕСЬ, что ReportGenerator использует double, а не decimal
                // Конвертируем в DTO для отображения
                SalesReport = new SalesReportDto
                {
                    StartDate = salesReport.StartDate,
                    EndDate = salesReport.EndDate,
                    GeneratedDate = salesReport.GeneratedDate,
                    TotalDeals = salesReport.TotalDeals,
                    CompletedDeals = salesReport.CompletedDeals,
                    PendingDeals = salesReport.PendingDeals,
                    TotalRevenue = salesReport.TotalRevenue,
                    AverageDealAmount = salesReport.AverageDealAmount,
                    AgentStatistics = salesReport.AgentStatistics.Select(a => new AgentStatisticsDto
                    {
                        AgentId = a.AgentId,
                        AgentName = a.AgentName,
                        TotalDeals = a.TotalDeals,
                        CompletedDeals = a.CompletedDeals,
                        TotalRevenue = a.TotalRevenue
                    }).ToList(),
                    MonthlyStatistics = salesReport.MonthlyStats.Select(m => new MonthlyStatisticsDto
                    {
                        Year = m.Year,
                        Month = m.Month,
                        DealCount = m.DealCount,
                        TotalRevenue = m.TotalRevenue,
                        AverageDealAmount = m.AverageDealAmount
                    }).ToList()
                };

                UpdateCharts();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации отчета: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
                ProgressText = string.Empty;
            }
        }
    }
}