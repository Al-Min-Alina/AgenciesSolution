using Agencies.Client.Services;
using Agencies.Client.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace Agencies.Client.Views
{
    public partial class ReportsWindow : Window
    {
        private readonly ReportsViewModel _viewModel;

        public ReportsWindow()
        {
            InitializeComponent();

            // Получите ApiService из контейнера зависимостей или создайте
            var apiService = new ApiService(); // Или используйте DI
            _viewModel = new ReportsViewModel(apiService);
            DataContext = _viewModel;
        }

        private async void BtnGenerateAll_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadAllReportsAsync();
        }

        private async void BtnRefreshSales_Click(object sender, RoutedEventArgs e)
        {
            await _viewModel.LoadSalesReportAsync();
        }

        private void BtnExportSales_Click(object sender, RoutedEventArgs e)
        {
            // Реализация экспорта в Excel
            MessageBox.Show("Экспорт в Excel (в разработке)");
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            // Экспорт в PDF
            MessageBox.Show("Экспорт в PDF (в разработке)");
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            // Печать
            MessageBox.Show("Печать (в разработке)");
        }
    }
}