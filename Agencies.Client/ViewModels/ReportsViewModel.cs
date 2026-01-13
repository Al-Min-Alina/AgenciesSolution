using Agencies.Client.Services;
using Agencies.Core.DTO;
using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.Windows;
using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Win32;
using System.Diagnostics;
using System.Text;
using System.Data;

namespace Agencies.Client.ViewModels
{
    public class ReportsViewModel : BaseViewModel
    {
        private readonly ApiService _apiService;
        private readonly ReportService _reportService;

        private SalesReportDto _salesReport;
        private PropertyAnalysisReportDto _propertyAnalysis;
        private ObservableCollection<AgentPerformanceDto> _agentPerformance;
        private DateTime _startDate = DateTime.Now.AddMonths(-1);
        private DateTime _endDate = DateTime.Now;
        private bool _isLoading;
        private string _progressText = "Готово";
        private SeriesCollection _monthlyRevenueSeries;
        private SeriesCollection _dealStatusSeries;
        private SeriesCollection _agentPerformanceSeries;
        private SeriesCollection _propertyTypeDistributionSeries;
        private SeriesCollection _averageDealSeries;
        private string[] _monthLabels;
        private string[] _agentLabels;

        public SalesReportDto SalesReport
        {
            get => _salesReport;
            set => SetProperty(ref _salesReport, value);
        }

        public PropertyAnalysisReportDto PropertyAnalysis
        {
            get => _propertyAnalysis;
            set => SetProperty(ref _propertyAnalysis, value);
        }

        public ObservableCollection<AgentPerformanceDto> AgentPerformance
        {
            get => _agentPerformance;
            set => SetProperty(ref _agentPerformance, value);
        }

        public DateTime StartDate
        {
            get => _startDate;
            set
            {
                if (SetProperty(ref _startDate, value))
                {
                    // Автоматически загружаем отчет при изменении даты
                    Task.Run(async () => await LoadSalesReportAsync());
                }
            }
        }

        public DateTime EndDate
        {
            get => _endDate;
            set
            {
                if (SetProperty(ref _endDate, value))
                {
                    // Автоматически загружаем отчет при изменении даты
                    Task.Run(async () => await LoadSalesReportAsync());
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        public SeriesCollection MonthlyRevenueSeries
        {
            get => _monthlyRevenueSeries;
            set => SetProperty(ref _monthlyRevenueSeries, value);
        }

        public SeriesCollection DealStatusSeries
        {
            get => _dealStatusSeries;
            set => SetProperty(ref _dealStatusSeries, value);
        }

        public SeriesCollection AgentPerformanceSeries
        {
            get => _agentPerformanceSeries;
            set => SetProperty(ref _agentPerformanceSeries, value);
        }

        public SeriesCollection PropertyTypeDistributionSeries
        {
            get => _propertyTypeDistributionSeries;
            set => SetProperty(ref _propertyTypeDistributionSeries, value);
        }

        public SeriesCollection AverageDealSeries
        {
            get => _averageDealSeries;
            set => SetProperty(ref _averageDealSeries, value);
        }

        public string[] MonthLabels
        {
            get => _monthLabels;
            set => SetProperty(ref _monthLabels, value);
        }

        public string[] AgentLabels
        {
            get => _agentLabels;
            set => SetProperty(ref _agentLabels, value);
        }

        public Func<double, string> CurrencyFormatter { get; set; }

        // Кеширование данных
        private Dictionary<DateTimeRange, SalesReportDto> _salesReportCache = new();

        private class DateTimeRange
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }

            public override bool Equals(object obj) =>
                obj is DateTimeRange other && Start == other.Start && End == other.End;

            public override int GetHashCode() => HashCode.Combine(Start, End);
        }

        public ReportsViewModel(ApiService apiService)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _reportService = new ReportService();

            InitializeCollections();
        }

        private void InitializeCollections()
        {
            SalesReport = new SalesReportDto();
            PropertyAnalysis = new PropertyAnalysisReportDto();
            AgentPerformance = new ObservableCollection<AgentPerformanceDto>();

            MonthlyRevenueSeries = new SeriesCollection();
            DealStatusSeries = new SeriesCollection();
            AgentPerformanceSeries = new SeriesCollection();
            PropertyTypeDistributionSeries = new SeriesCollection();
            AverageDealSeries = new SeriesCollection();

            MonthLabels = Array.Empty<string>();
            AgentLabels = Array.Empty<string>();

            // Форматтер для долларов
            CurrencyFormatter = value =>
            {
                if (value >= 1000000)
                    return $"{(value / 1000000):0.#} млн $";
                if (value >= 1000)
                    return $"{(value / 1000):0.#} тыс $";

                return $"{value:0} $";
            };
        }

        public async Task LoadAllReportsAsync()
        {
            try
            {
                IsLoading = true;
                ProgressText = "Загрузка всех отчетов...";

                // Загружаем все отчеты параллельно
                var tasks = new List<Task>
                {
                    LoadSalesReportAsync(),
                    LoadPropertyAnalysisAsync(),
                    LoadAgentPerformanceAsync()
                };

                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка загрузки отчетов: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                ProgressText = "Готово";
            }
        }

        public async Task LoadSalesReportAsync(bool forceRefresh = false)
        {
            var range = new DateTimeRange { Start = StartDate, End = EndDate };

            if (!forceRefresh && _salesReportCache.TryGetValue(range, out var cachedReport))
            {
                SalesReport = cachedReport;
                UpdateSalesCharts();
                return;
            }

            try
            {
                ProgressText = "Генерация отчета по продажам...";
                IsLoading = true;

                // Получаем данные параллельно
                var dealsTask = _apiService.GetDealsAsync();
                var propertiesTask = _apiService.GetPropertiesAsync();
                var clientsTask = _apiService.GetClientsAsync();

                await Task.WhenAll(dealsTask, propertiesTask, clientsTask).ConfigureAwait(false);

                var deals = await dealsTask;
                var properties = await propertiesTask;
                var clients = await clientsTask;

                // Обновляем данные в ReportService
                _reportService.UpdateData(deals, properties, clients);

                // Генерируем отчет
                SalesReport = await _reportService.GenerateSalesReportAsync(StartDate, EndDate).ConfigureAwait(false);

                // Обновляем графики в UI потоке
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    UpdateSalesCharts();
                });

                // Кешируем результат
                _salesReportCache[range] = SalesReport;
            }
            catch (Exception ex)
            {
                LogError($"Ошибка загрузки отчета: {ex}");
                ShowError($"Ошибка генерации отчета: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                ProgressText = "Готово";
            }
        }

        public async Task LoadPropertyAnalysisAsync()
        {
            try
            {
                ProgressText = "Анализ объектов недвижимости...";
                IsLoading = true;

                var properties = await _apiService.GetPropertiesAsync();
                var deals = await _apiService.GetDealsAsync();

                _reportService.UpdateData(deals, properties, new List<ClientDto>());
                PropertyAnalysis = await _reportService.GeneratePropertyAnalysisReportAsync();

                UpdatePropertyCharts();

                ProgressText = "Анализ объектов готов";
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка анализа объектов: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task LoadAgentPerformanceAsync()
        {
            try
            {
                ProgressText = "Анализ эффективности агентов...";
                IsLoading = true;

                var deals = await _apiService.GetDealsAsync();
                var clients = await _apiService.GetClientsAsync();
                var properties = await _apiService.GetPropertiesAsync();

                _reportService.UpdateData(deals, properties, clients);
                var reports = await _reportService.GenerateAgentPerformanceReportAsync(StartDate, EndDate);

                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    AgentPerformance.Clear();
                    foreach (var report in reports)
                    {
                        AgentPerformance.Add(report);
                    }
                });

                ProgressText = "Анализ агентов готов";
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка анализа агентов: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private bool HasData<T>(IEnumerable<T> collection) =>
            collection?.Any() == true;

        private void UpdateSalesCharts()
        {
            if (Application.Current?.Dispatcher == null) return;

            Application.Current.Dispatcher.Invoke(() =>
            {
                if (!HasData(SalesReport?.MonthlyStatistics))
                {
                    // Показываем заглушку
                    MonthlyRevenueSeries = new SeriesCollection
                    {
                        new ColumnSeries
                        {
                            Title = "Нет данных",
                            Values = new ChartValues<double> { 0 },
                            Fill = Brushes.LightGray
                        }
                    };
                    MonthLabels = new[] { "Нет данных" };

                    OnPropertyChanged(nameof(MonthlyRevenueSeries));
                    OnPropertyChanged(nameof(MonthLabels));
                    return;
                }

                UpdateMonthlyRevenueChart();
                UpdateAverageDealChart();
                UpdateDealStatusChart();
                UpdateAgentPerformanceChart();
            });
        }

        private void UpdateMonthlyRevenueChart()
        {
            if (SalesReport?.MonthlyStatistics == null || !SalesReport.MonthlyStatistics.Any())
            {
                MonthlyRevenueSeries.Clear();
                return;
            }

            MonthlyRevenueSeries.Clear();

            var columnSeries = new ColumnSeries
            {
                Title = "Выручка",
                Values = new ChartValues<double>(
                    SalesReport.MonthlyStatistics.Select(m => (double)m.TotalRevenue)),
                Fill = new SolidColorBrush(Color.FromRgb(52, 152, 219)),
                Stroke = new SolidColorBrush(Color.FromRgb(41, 128, 185)),
                StrokeThickness = 1,
                DataLabels = true,
                LabelPoint = point => CurrencyFormatter((double)point.Y),
                MaxColumnWidth = 40
            };

            MonthlyRevenueSeries.Add(columnSeries);

            MonthLabels = SalesReport.MonthlyStatistics
                .Select(m =>
                {
                    if (DateTime.TryParseExact(m.MonthName, "MMMM yyyy", CultureInfo.CurrentCulture,
                        DateTimeStyles.None, out var date))
                    {
                        return CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(date.Month);
                    }
                    return m.MonthName;
                })
                .ToArray();

            OnPropertyChanged(nameof(MonthlyRevenueSeries));
            OnPropertyChanged(nameof(MonthLabels));
        }

        private void UpdateAverageDealChart()
        {
            try
            {
                // Проверяем, что SalesReport и MonthlyStatistics не null
                if (SalesReport == null ||
                    SalesReport.MonthlyStatistics == null ||
                    !SalesReport.MonthlyStatistics.Any())
                {
                    // Инициализируем AverageDealSeries, если она null
                    if (AverageDealSeries == null)
                    {
                        AverageDealSeries = new SeriesCollection();
                    }
                    else
                    {
                        AverageDealSeries.Clear();
                    }
                    return;
                }

                // Инициализируем AverageDealSeries, если она null
                if (AverageDealSeries == null)
                {
                    AverageDealSeries = new SeriesCollection();
                }
                else
                {
                    AverageDealSeries.Clear();
                }

                // Получаем данные для графика
                var dataPoints = SalesReport.MonthlyStatistics
                    .Select(m => (double)m.AverageDealAmount)
                    .ToList();

                if (!dataPoints.Any())
                {
                    return;
                }

                // Создаем линейный график
                var lineSeries = new LineSeries
                {
                    Title = "Средняя сумма сделки",
                    Values = new ChartValues<double>(dataPoints),
                    Stroke = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                    StrokeThickness = 2,
                    Fill = new LinearGradientBrush
                    {
                        StartPoint = new Point(0, 0),
                        EndPoint = new Point(0, 1),
                        GradientStops = new GradientStopCollection
                        {
                            new GradientStop(Color.FromArgb(100, 231, 76, 60), 0),
                            new GradientStop(Colors.Transparent, 1)
                        }
                    },
                    // Используем Geometry для точек
                    PointGeometry = Geometry.Parse("M 0,0 L 10,0 L 5,10 Z"), // треугольник
                    PointGeometrySize = 10,
                    PointForeground = Brushes.White
                };

                AverageDealSeries.Add(lineSeries);
                OnPropertyChanged(nameof(AverageDealSeries));
            }
            catch (Exception ex)
            {
                // Логируем ошибку
                LogError($"Ошибка в UpdateAverageDealChart: {ex.Message}");

                // Создаем пустой график в случае ошибки
                AverageDealSeries = new SeriesCollection();
                OnPropertyChanged(nameof(AverageDealSeries));
            }
        }

        // Альтернативный вариант с более простой реализацией
        private void UpdateAverageDealChartSimple()
        {
            try
            {
                // Безопасная проверка на null
                if (SalesReport?.MonthlyStatistics?.Any() != true)
                {
                    AverageDealSeries?.Clear();
                    AverageDealSeries = AverageDealSeries ?? new SeriesCollection();
                    return;
                }

                AverageDealSeries = new SeriesCollection
                {
                    new LineSeries
                    {
                        Title = "Средняя сумма сделки",
                        Values = new ChartValues<double>(
                            SalesReport.MonthlyStatistics.Select(m => (double)m.AverageDealAmount)),
                        Stroke = new SolidColorBrush(Color.FromRgb(231, 76, 60)),
                        StrokeThickness = 2,
                        PointGeometry = null, // Без точек для простоты
                        DataLabels = true,
                        LabelPoint = point => CurrencyFormatter((double)point.Y)
                    }
                };

                OnPropertyChanged(nameof(AverageDealSeries));
            }
            catch (Exception ex)
            {
                LogError($"Ошибка в UpdateAverageDealChartSimple: {ex.Message}");
                AverageDealSeries = new SeriesCollection();
                OnPropertyChanged(nameof(AverageDealSeries));
            }
        }

        private void UpdateDealStatusChart()
        {
            if (SalesReport == null)
                return;

            DealStatusSeries.Clear();

            var statusData = new[]
            {
                new { Status = "Завершено", Count = SalesReport.CompletedDeals, Color = Color.FromRgb(46, 204, 113) },
                new { Status = "В ожидании", Count = SalesReport.PendingDeals, Color = Color.FromRgb(241, 196, 15) },
                new { Status = "Отменено", Count = SalesReport.CancelledDeals, Color = Color.FromRgb(231, 76, 60) }
            };

            foreach (var status in statusData.Where(s => s.Count > 0))
            {
                var pieSeries = new PieSeries
                {
                    Title = status.Status,
                    Values = new ChartValues<double> { status.Count },
                    DataLabels = true,
                    LabelPoint = point => $"{point.Y} ({point.Participation:P0})",
                    Fill = new SolidColorBrush(status.Color)
                };

                DealStatusSeries.Add(pieSeries);
            }

            OnPropertyChanged(nameof(DealStatusSeries));
        }

        private void UpdateAgentPerformanceChart()
        {
            if (SalesReport?.AgentStatistics == null)
                return;

            AgentPerformanceSeries.Clear();

            // Берем топ 10 агентов
            var topAgents = SalesReport.AgentStatistics
                .OrderByDescending(a => a.TotalRevenue)
                .Take(10)
                .ToList();

            AgentLabels = topAgents.Select(a => a.AgentName).ToArray();

            var columnSeries = new ColumnSeries
            {
                Title = "Выручка агентов",
                Values = new ChartValues<double>(
                    topAgents.Select(a => (double)a.TotalRevenue)),
                Fill = new SolidColorBrush(Color.FromRgb(155, 89, 182)),
                DataLabels = true,
                LabelPoint = point => CurrencyFormatter((double)point.Y),
                MaxColumnWidth = 30
            };

            AgentPerformanceSeries.Add(columnSeries);
            OnPropertyChanged(nameof(AgentPerformanceSeries));
            OnPropertyChanged(nameof(AgentLabels));
        }

        private void UpdatePropertyCharts()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                UpdatePropertyTypeDistributionChart();
            });
        }

        private void UpdatePropertyTypeDistributionChart()
        {
            if (PropertyAnalysis?.PropertyTypeAnalysis == null)
                return;

            PropertyTypeDistributionSeries.Clear();

            var colors = new[]
            {
                Color.FromRgb(52, 152, 219),    // Синий
                Color.FromRgb(46, 204, 113),    // Зеленый
                Color.FromRgb(155, 89, 182),    // Фиолетовый
                Color.FromRgb(241, 196, 15),    // Желтый
                Color.FromRgb(230, 126, 34),    // Оранжевый
                Color.FromRgb(231, 76, 60),     // Красный
                Color.FromRgb(149, 165, 166)    // Серый
            };

            int colorIndex = 0;
            foreach (var type in PropertyAnalysis.PropertyTypeAnalysis)
            {
                var color = colors[colorIndex % colors.Length];
                var pieSeries = new PieSeries
                {
                    Title = type.PropertyType,
                    Values = new ChartValues<double> { type.Count },
                    DataLabels = true,
                    LabelPoint = point => $"{type.PropertyType}: {point.Y} ({point.Participation:P0})",
                    Fill = new SolidColorBrush(color)
                };

                PropertyTypeDistributionSeries.Add(pieSeries);
                colorIndex++;
            }

            OnPropertyChanged(nameof(PropertyTypeDistributionSeries));
        }

        private void ShowError(string message)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        private void LogError(string message)
        {
            System.Diagnostics.Trace.TraceError(message);
        }

        public async Task<bool> ExportToPdfAsync(string filePath)
        {
            try
            {
                // Проверяем, есть ли данные для экспорта
                if (SalesReport == null ||
                    (SalesReport.MonthlyStatistics == null || !SalesReport.MonthlyStatistics.Any()))
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("Нет данных для экспорта в PDF. Сначала загрузите отчет.",
                            "Нет данных", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    return false;
                }

                // Создаем документ PDF с помощью iTextSharp
                using (FileStream stream = new FileStream(filePath, FileMode.Create))
                {
                    // Создаем документ A4 с полями
                    Document document = new Document(PageSize.A4, 50, 50, 50, 50);
                    PdfWriter writer = PdfWriter.GetInstance(document, stream);

                    document.Open();

                    // Добавляем заголовок
                    var titleFont = FontFactory.GetFont("Arial", 18, Font.BOLD, new BaseColor(0, 0, 255)); // Синий цвет
                    var headerFont = FontFactory.GetFont("Arial", 12, Font.BOLD,  new BaseColor(0, 0, 255));
                    var normalFont = FontFactory.GetFont("Arial", 10, Font.NORMAL, new BaseColor(0, 0, 255));
                    var boldFont = FontFactory.GetFont("Arial", 10, Font.BOLD, new BaseColor(0, 0, 255));

                    // Заголовок отчета
                    Paragraph title = new Paragraph("Отчет по продажам агентства недвижимости", titleFont);
                    title.Alignment = Element.ALIGN_CENTER;
                    title.SpacingAfter = 20f;
                    document.Add(title);

                    // Период отчета
                    Paragraph period = new Paragraph($"Период: {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}", boldFont);
                    period.SpacingAfter = 10f;
                    document.Add(period);

                    // Дата генерации
                    Paragraph generatedDate = new Paragraph($"Сгенерировано: {DateTime.Now:dd.MM.yyyy HH:mm}", normalFont);
                    generatedDate.SpacingAfter = 20f;
                    document.Add(generatedDate);

                    // Общая статистика
                    Paragraph statsTitle = new Paragraph("Общая статистика", headerFont);
                    statsTitle.SpacingAfter = 10f;
                    document.Add(statsTitle);

                    // Создаем таблицу для статистики
                    PdfPTable statsTable = new PdfPTable(2);
                    statsTable.WidthPercentage = 100;
                    statsTable.SetWidths(new float[] { 1f, 1f });

                    AddStatRow(statsTable, "Общая выручка:", $"{SalesReport.TotalRevenue:C}", boldFont, normalFont);
                    AddStatRow(statsTable, "Всего сделок:", $"{SalesReport.TotalDeals}", boldFont, normalFont);
                    AddStatRow(statsTable, "Завершено сделок:", $"{SalesReport.CompletedDeals}", boldFont, normalFont);
                    AddStatRow(statsTable, "В ожидании:", $"{SalesReport.PendingDeals}", boldFont, normalFont);
                    AddStatRow(statsTable, "Отменено сделок:", $"{SalesReport.CancelledDeals}", boldFont, normalFont);
                    AddStatRow(statsTable, "Средняя сумма сделки:", $"{SalesReport.AverageDealAmount:C}", boldFont, normalFont);

                    document.Add(statsTable);
                    document.Add(new Paragraph(" "));

                    // Статистика по месяцам
                    if (SalesReport.MonthlyStatistics != null && SalesReport.MonthlyStatistics.Any())
                    {
                        Paragraph monthlyTitle = new Paragraph("Статистика по месяцам", headerFont);
                        monthlyTitle.SpacingAfter = 10f;
                        document.Add(monthlyTitle);

                        // Создаем таблицу для месячной статистики
                        PdfPTable monthlyTable = new PdfPTable(4);
                        monthlyTable.WidthPercentage = 100;
                        monthlyTable.SetWidths(new float[] { 1.5f, 1f, 1f, 1f });

                        // Заголовки таблицы
                        AddTableHeader(monthlyTable, "Месяц", headerFont);
                        AddTableHeader(monthlyTable, "Выручка", headerFont);
                        AddTableHeader(monthlyTable, "Средняя сделка", headerFont);
                        AddTableHeader(monthlyTable, "Кол-во сделок", headerFont);

                        // Данные
                        foreach (var month in SalesReport.MonthlyStatistics)
                        {
                            monthlyTable.AddCell(new PdfPCell(new Phrase(month.MonthName, normalFont)));
                            monthlyTable.AddCell(new PdfPCell(new Phrase($"{month.TotalRevenue:C}", normalFont)));
                            monthlyTable.AddCell(new PdfPCell(new Phrase($"{month.AverageDealAmount:C}", normalFont)));
                            monthlyTable.AddCell(new PdfPCell(new Phrase(month.DealCount.ToString(), normalFont)));
                        }

                        document.Add(monthlyTable);
                        document.Add(new Paragraph(" "));
                    }

                    // Статистика по агентам
                    if (SalesReport.AgentStatistics != null && SalesReport.AgentStatistics.Any())
                    {
                        Paragraph agentsTitle = new Paragraph("Статистика по агентам", headerFont);
                        agentsTitle.SpacingAfter = 10f;
                        document.Add(agentsTitle);

                        PdfPTable agentsTable = new PdfPTable(4);
                        agentsTable.WidthPercentage = 100;
                        agentsTable.SetWidths(new float[] { 2f, 1f, 1f, 1f });

                        AddTableHeader(agentsTable, "Агент", headerFont);
                        AddTableHeader(agentsTable, "Всего сделок", headerFont);
                        AddTableHeader(agentsTable, "Завершено", headerFont);
                        AddTableHeader(agentsTable, "Выручка", headerFont);

                        foreach (var agent in SalesReport.AgentStatistics.OrderByDescending(a => a.TotalRevenue))
                        {
                            agentsTable.AddCell(new PdfPCell(new Phrase(agent.AgentName, normalFont)));
                            agentsTable.AddCell(new PdfPCell(new Phrase(agent.TotalDeals.ToString(), normalFont)));
                            agentsTable.AddCell(new PdfPCell(new Phrase(agent.CompletedDeals.ToString(), normalFont)));
                            agentsTable.AddCell(new PdfPCell(new Phrase($"{agent.TotalRevenue:C}", normalFont)));
                        }

                        document.Add(agentsTable);
                        document.Add(new Paragraph(" "));
                    }

                    // Топ объектов недвижимости
                    if (SalesReport.TopProperties != null && SalesReport.TopProperties.Any())
                    {
                        Paragraph propertiesTitle = new Paragraph("Топ объектов недвижимости", headerFont);
                        propertiesTitle.SpacingAfter = 10f;
                        document.Add(propertiesTitle);

                        PdfPTable propertiesTable = new PdfPTable(3);
                        propertiesTable.WidthPercentage = 100;
                        propertiesTable.SetWidths(new float[] { 2f, 1f, 1f });

                        AddTableHeader(propertiesTable, "Объект", headerFont);
                        AddTableHeader(propertiesTable, "Кол-во сделок", headerFont);
                        AddTableHeader(propertiesTable, "Общая выручка", headerFont);

                        foreach (var property in SalesReport.TopProperties)
                        {
                            propertiesTable.AddCell(new PdfPCell(new Phrase(property.PropertyTitle, normalFont)));
                            propertiesTable.AddCell(new PdfPCell(new Phrase(property.DealCount.ToString(), normalFont)));
                            propertiesTable.AddCell(new PdfPCell(new Phrase($"{property.TotalRevenue:C}", normalFont)));
                        }

                        document.Add(propertiesTable);
                    }

                    // Добавляем нумерацию страниц
                    writer.PageEvent = new PdfPageEventHelper();

                    document.Close();
                }

                // Проверяем, что файл создан и не пустой
                return File.Exists(filePath) && new FileInfo(filePath).Length > 0;
            }
            catch (Exception ex)
            {
                LogError($"Ошибка при создании PDF: {ex}");

                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Ошибка при создании PDF: {ex.Message}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                });

                return false;
            }
        }

        private void AddStatRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            table.AddCell(new PdfPCell(new Phrase(label, labelFont)));
            table.AddCell(new PdfPCell(new Phrase(value, valueFont)));
        }

        private void AddTableHeader(PdfPTable table, string text, Font font)
        {
            PdfPCell cell = new PdfPCell(new Phrase(text, font));
            cell.BackgroundColor = new BaseColor(240, 240, 240);
            cell.HorizontalAlignment = Element.ALIGN_CENTER;
            cell.Padding = 5;
            table.AddCell(cell);
        }

        public async Task ExportToExcelAsync()
        {
            try
            {
                ProgressText = "Экспорт в Excel...";
                IsLoading = true;

                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "Excel файлы (*.xlsx)|*.xlsx|CSV файлы (*.csv)|*.csv",
                    FileName = $"Отчет_продаж_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
                    DefaultExt = ".xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    var success = false;

                    if (saveFileDialog.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        success = await ExportToCsvAsync(SalesReport, saveFileDialog.FileName);
                    }
                    else
                    {
                        success = await ExportToExcelFileAsync(SalesReport, saveFileDialog.FileName);
                    }

                    if (success)
                    {
                        MessageBox.Show($"Отчет успешно экспортирован в:\n{saveFileDialog.FileName}",
                            "Успешно", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка экспорта: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
                ProgressText = "Готово";
            }
        }

        private async Task<bool> ExportToCsvAsync(SalesReportDto report, string filePath)
        {
            try
            {
                using (var writer = new StreamWriter(filePath, false, Encoding.UTF8))
                {
                    // Заголовок
                    await writer.WriteLineAsync($"Отчет по продажам за период: {StartDate:dd.MM.yyyy} - {EndDate:dd.MM.yyyy}");
                    await writer.WriteLineAsync();

                    // Основная статистика
                    await writer.WriteLineAsync("Общая статистика:");
                    await writer.WriteLineAsync($"Общая выручка;{report.TotalRevenue:C}");
                    await writer.WriteLineAsync($"Всего сделок;{report.TotalDeals}");
                    await writer.WriteLineAsync($"Завершено сделок;{report.CompletedDeals}");
                    await writer.WriteLineAsync($"Средняя сумма;{report.AverageDealAmount:C}");
                    await writer.WriteLineAsync();

                    // Статистика по агентам
                    if (report.AgentStatistics?.Any() == true)
                    {
                        await writer.WriteLineAsync("Статистика по агентам:");
                        await writer.WriteLineAsync("Агент;Всего сделок;Завершено;Выручка");
                        foreach (var agent in report.AgentStatistics)
                        {
                            await writer.WriteLineAsync($"{agent.AgentName};{agent.TotalDeals};{agent.CompletedDeals};{agent.TotalRevenue:C}");
                        }
                        await writer.WriteLineAsync();
                    }

                    // Статистика по месяцам
                    if (report.MonthlyStatistics?.Any() == true)
                    {
                        await writer.WriteLineAsync("Статистика по месяцам:");
                        await writer.WriteLineAsync("Месяц;Выручка;Средняя сделка;Количество сделок");
                        foreach (var month in report.MonthlyStatistics)
                        {
                            await writer.WriteLineAsync($"{month.MonthName};{month.TotalRevenue:C};{month.AverageDealAmount:C};{month.DealCount}");
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка экспорта в CSV: {ex.Message}");
                return false;
            }
        }

        private async Task<bool> ExportToExcelFileAsync(SalesReportDto report, string filePath)
        {
            try
            {
                // Вместо этого метода используйте библиотеку типа EPPlus, ClosedXML или Microsoft.Office.Interop.Excel
                // Здесь пример с EPPlus (нужно установить пакет EPPlus)

                /*
                using (var package = new OfficeOpenXml.ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Отчет по продажам");
                    
                    // Заголовок
                    worksheet.Cells[1, 1].Value = "Отчет по продажам";
                    worksheet.Cells[1, 1].Style.Font.Bold = true;
                    worksheet.Cells[1, 1].Style.Font.Size = 14;
                    
                    // и т.д.
                    
                    package.SaveAs(new FileInfo(filePath));
                }
                */

                // Временно используем CSV, если не установлена библиотека Excel
                return await ExportToCsvAsync(report, filePath.Replace(".xlsx", ".csv"));
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка экспорта в Excel: {ex.Message}");
                return false;
            }
        }

        // Тема графиков (опционально)
        public enum ChartTheme
        {
            Light,
            Dark,
            Corporate
        }

        private ChartTheme _currentTheme = ChartTheme.Light;

        public ChartTheme CurrentTheme
        {
            get => _currentTheme;
            set
            {
                if (SetProperty(ref _currentTheme, value))
                {
                    ApplyChartTheme();
                }
            }
        }

        private void ApplyChartTheme()
        {
            var colors = _currentTheme switch
            {
                ChartTheme.Dark => new[]
                {
                    Color.FromRgb(86, 98, 246),  // Синий
                    Color.FromRgb(34, 197, 94),  // Зеленый
                    Color.FromRgb(234, 179, 8),  // Желтый
                    Color.FromRgb(239, 68, 68)   // Красный
                },
                ChartTheme.Corporate => new[]
                {
                    Color.FromRgb(0, 82, 147),   // Темно-синий
                    Color.FromRgb(0, 131, 143),  // Бирюзовый
                    Color.FromRgb(245, 130, 32), // Оранжевый
                    Color.FromRgb(114, 193, 240) // Светло-синий
                },
                _ => new[]
                {
                    Color.FromRgb(52, 152, 219),
                    Color.FromRgb(46, 204, 113),
                    Color.FromRgb(241, 196, 15),
                    Color.FromRgb(231, 76, 60)
                }
            };

            // Обновляем все серии с новыми цветами
            // Реализуйте обновление цветов в графиках
        }
    }
}