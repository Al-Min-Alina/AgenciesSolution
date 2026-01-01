using System;
using System.Windows;
using System.Windows.Controls;
using System.IO;
using System.Linq;

namespace Agencies.Client.Views
{
    public partial class LogViewerWindow : Window
    {
        public LogViewerWindow()
        {
            InitializeComponent();
            // В примере используется привязка данных. Если у вас есть ViewModel,
            // убедитесь, что она установлена в качестве DataContext (например, в конструкторе).
            // DataContext = new LogViewerViewModel();
        }

        // Пример коллекции для привязки (замените на вашу ViewModel)
        public System.Collections.ObjectModel.ObservableCollection<LogEntry> LogEntries { get; set; }

        // Обработчик для кнопки "Обновить"
        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            // 1. Здесь должна быть логика загрузки или обновления логов.
            // Пример:
            // var logs = await _logService.GetLogsAsync();
            // LogEntries.Clear();
            // foreach (var log in logs) LogEntries.Add(log);

            // 2. Для примера просто обновим статус
            tbStatus.Text = $"Логи обновлены: {DateTime.Now:HH:mm:ss}";
        }

        // Обработчик для кнопки "Очистить"
        private void BtnClear_Click(object sender, RoutedEventArgs e)
        {
            if (LogEntries != null)
            {
                LogEntries.Clear();
            }
            tbStatus.Text = "Список логов очищен.";
        }

        // Обработчик для кнопки "Сохранить в файл"
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Filter = "Текстовый файл (*.txt)|*.txt|Все файлы (*.*)|*.*",
                DefaultExt = ".txt",
                FileName = $"logs_{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    // Пример сохранения: запись всех сообщений в файл
                    using (var writer = new StreamWriter(saveDialog.FileName))
                    {
                        if (LogEntries != null)
                        {
                            foreach (var entry in LogEntries)
                            {
                                writer.WriteLine($"{entry.Timestamp:HH:mm:ss} [{entry.Level}] {entry.Message}");
                            }
                        }
                    }
                    tbStatus.Text = $"Логи сохранены в файл: {saveDialog.FileName}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка при сохранении файла:\n{ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // Обработчик для поля поиска (срабатывает при изменении текста)
        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 1. Получаем текст для поиска
            var searchText = txtSearch.Text?.Trim();

            // 2. Если используется ViewModel с поддержкой фильтрации, вызываем соответствующий метод.
            // Пример:
            // if (DataContext is LogViewerViewModel viewModel)
            // {
            //     viewModel.FilterLogs(searchText);
            // }

            // 3. Простой пример для коллекции в коде окна:
            if (string.IsNullOrEmpty(searchText))
            {
                // Сброс фильтра (если используется CollectionViewSource)
                // Если LogItemsView является CollectionViewSource, можно так:
                // LogItemsView.View.Filter = null;
                tbStatus.Text = "Поиск отключен.";
            }
            else
            {
                // Применение фильтра по вхождению строки в сообщение или источник
                // Если LogItemsView является CollectionViewSource:
                // LogItemsView.View.Filter = item => 
                // {
                //     var log = item as LogEntry;
                //     return log.Message.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
                //            || log.Source.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
                // };
                tbStatus.Text = $"Поиск: '{searchText}'";
            }
        }
    }

    // Пример класса модели для элемента лога (если ещё нет)
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } // Например: "Info", "Error", "Warning"
        public string Message { get; set; }
        public string Source { get; set; } // Например: "Agencies.API.Controllers.PropertiesController"
    }
}