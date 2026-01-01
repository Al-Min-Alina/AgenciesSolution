using Agencies.Client.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace Agencies.Client
{
    public partial class App : Application
    {
        public static ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();
        public static string LogFilePath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Agencies",
            "logs",
            $"log_{DateTime.Now:yyyyMMdd}.txt");

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Создаем директорию для логов
            Directory.CreateDirectory(Path.GetDirectoryName(LogFilePath));

            // Настраиваем глобальную обработку исключений
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;

            // Инициализируем сервисы
            InitializeServices();
        }

        private void InitializeServices()
        {
            // Можно использовать DI контейнер в реальном проекте
            var errorHandler = new WpfErrorHandler(LogMessage);

            // Сохраняем в ресурсы приложения
            Resources.Add("ErrorHandler", errorHandler);
        }

        private void LogMessage(string message)
        {
            Dispatcher.Invoke(() =>
            {
                var entry = new LogEntry
                {
                    Timestamp = DateTime.Now,
                    Level = "INFO",
                    Message = message,
                    Source = "Application"
                };

                LogEntries.Add(entry);

                // Сохраняем в файл
                File.AppendAllText(LogFilePath, $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss} [{entry.Level}] {entry.Message}\n");

                // Ограничиваем размер коллекции
                if (LogEntries.Count > 1000)
                {
                    LogEntries.RemoveAt(0);
                }
            });
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                HandleUnhandledException(ex);
            }
        }

        private void App_DispatcherUnhandledException(object sender,
            System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            HandleUnhandledException(e.Exception);
            e.Handled = true; // Предотвращаем краш приложения
        }

        private void HandleUnhandledException(Exception ex)
        {
            var errorHandler = Resources["ErrorHandler"] as WpfErrorHandler;
            errorHandler?.LogError(ex, "Необработанное исключение");

            // Показываем дружелюбное сообщение пользователю
            MessageBox.Show(
                "Произошла непредвиденная ошибка. Приложение будет закрыто.\n\n" +
                "Детали ошибки были сохранены в журнале.",
                "Критическая ошибка",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Записываем в файл
            File.AppendAllText(LogFilePath,
                $"[FATAL] {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n{ex}\n\n");

            Environment.Exit(1);
        }
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string Source { get; set; }
        public string StackTrace { get; set; }
    }
}