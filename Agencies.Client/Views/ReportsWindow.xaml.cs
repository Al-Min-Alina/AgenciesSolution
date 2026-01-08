using Agencies.Client.Services;
using Agencies.Client.ViewModels;
using System;
using System.Windows;
using System.Windows.Controls;

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
            await _viewModel.ExportToPdfAsync();
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            // Реализация печати
            MessageBox.Show("Печать отчета (в разработке)", "Информация",
                MessageBoxButton.OK, MessageBoxImage.Information);
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