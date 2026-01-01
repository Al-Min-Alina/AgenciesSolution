using System;
using System.Net;
using System.Windows;

namespace Agencies.Client.Services
{
    public class WpfErrorHandler : IErrorHandler
    {
        private readonly Action<string> _logAction;

        public WpfErrorHandler(Action<string> logAction = null)
        {
            _logAction = logAction;
        }

        public void LogError(Exception ex, string message)
        {
            var fullMessage = $"{message}: {ex.Message}";

            // Логируем в консоль
            Console.WriteLine($"[ERROR] {fullMessage}");
            Console.WriteLine(ex.StackTrace);

            // Вызываем пользовательское логирование
            _logAction?.Invoke(fullMessage);

            // Показываем диалог только для критических ошибок
            if (IsCriticalError(ex))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ShowError(fullMessage, "Критическая ошибка");
                });
            }
        }

        public void LogWarning(string message)
        {
            Console.WriteLine($"[WARNING] {message}");
            _logAction?.Invoke(message);
        }

        public void ShowError(string message, string title = "Ошибка")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
            });
        }

        public void ShowWarning(string message, string title = "Предупреждение")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        public void ShowInfo(string message, string title = "Информация")
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
            });
        }

        private bool IsCriticalError(Exception ex)
        {
            return ex is ApiException apiEx &&
                  (apiEx.StatusCode == HttpStatusCode.InternalServerError ||
                   apiEx.StatusCode == HttpStatusCode.ServiceUnavailable);
        }
    }
}