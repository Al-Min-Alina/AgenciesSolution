using Agencies.Client.Services;
using Agencies.Client.ViewModels;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace Agencies.Client.Views
{
    public partial class ReportsWindow : Window
    {
        private readonly ReportsViewModel _viewModel;

        public ReportsWindow(ApiService apiService)
        {
            InitializeComponent();

            if (apiService == null)
                throw new ArgumentNullException(nameof(apiService));

            _viewModel = new ReportsViewModel(apiService);
            DataContext = _viewModel;

            Loaded += async (s, e) =>
            {
                // Автоматически загружаем отчет по продажам при открытии окна
                await _viewModel.LoadSalesReportAsync();
            };
        }

        private async void BtnGenerateAll_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadAllReportsAsync();
        }

        private async void BtnRefreshSales_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadSalesReportAsync();
        }

        private async void BtnExportSales_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _viewModel.ExportToExcelAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Предлагаем пользователю выбрать место сохранения
                var saveFileDialog = new SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf",
                    DefaultExt = "pdf",
                    FileName = $"Отчет_Агентства_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                    Title = "Сохранить отчет в PDF"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string filePath = saveFileDialog.FileName;

                    // Показываем индикатор загрузки
                    btnExport.IsEnabled = false;
                    btnExport.Content = "Экспорт...";

                    try
                    {
                        bool success = await _viewModel.ExportToPdfAsync(filePath);

                        if (success)
                        {
                            MessageBox.Show($"Отчет успешно сохранен:\n{filePath}",
                                "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                            // Предлагаем открыть файл
                            var result = MessageBox.Show("Хотите открыть полученный PDF-файл?",
                                "Открыть файл", MessageBoxButton.YesNo, MessageBoxImage.Question);

                            if (result == MessageBoxResult.Yes)
                            {
                                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                                {
                                    FileName = filePath,
                                    UseShellExecute = true
                                });
                            }
                        }
                        else
                        {
                            MessageBox.Show("Не удалось создать PDF-отчет. Проверьте данные и повторите попытку.",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    finally
                    {
                        // Восстанавливаем кнопку
                        btnExport.IsEnabled = true;
                        btnExport.Content = "Экспорт в PDF";
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                MessageBox.Show("Отказано в доступе к указанному пути. Попробуйте выбрать другую папку.",
                    "Ошибка доступа", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException ioEx)
            {
                MessageBox.Show($"Ошибка ввода-вывода: {ioEx.Message}\nФайл может быть занят другим процессом.",
                    "Ошибка файла", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в PDF: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Создаем PDF во временный файл и печатаем
                string tempFile = Path.GetTempFileName() + ".pdf";

                // Здесь нужно добавить логику создания PDF для печати
                // Для примера - просто показываем диалог печати
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    // Можно добавить логику печати данных отчета
                    MessageBox.Show("Печать отчета (функционал в разработке)",
                        "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при печати: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void CbDateRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Проверяем, что _viewModel не null
            if (_viewModel == null)
                return;

            // Быстрая настройка диапазона дат
            if (sender is ComboBox comboBox && comboBox.SelectedItem != null)
            {
                var now = DateTime.Now;
                var selectedItem = comboBox.SelectedItem as ComboBoxItem;

                if (selectedItem?.Tag != null)
                {
                    switch (selectedItem.Tag.ToString())
                    {
                        case "week":
                            _viewModel.StartDate = now.AddDays(-7);
                            _viewModel.EndDate = now;
                            break;
                        case "month":
                            _viewModel.StartDate = now.AddMonths(-1);
                            _viewModel.EndDate = now;
                            break;
                        case "quarter":
                            _viewModel.StartDate = now.AddMonths(-3);
                            _viewModel.EndDate = now;
                            break;
                        case "year":
                            _viewModel.StartDate = now.AddYears(-1);
                            _viewModel.EndDate = now;
                            break;
                    }

                    await _viewModel.LoadSalesReportAsync();
                }
            }
        }
    }
}