using Agencies.Client.Services;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Net.Http.Headers;
using System.Net.Http;
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

        public static ApiService ApiService { get; private set; }
        public static string CurrentUser { get; set; }
        public static string UserRole { get; set; }
        public static DateTime TokenExpiry { get; set; }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            InitializeApiService();
            RestoreSession();

        }

        private void InitializeApiService()
        {
            try
            {
                ApiService = new ApiService("https://localhost:7149/api/");

                ApiService.OnSessionExpired += OnSessionExpired;

                LogMessage("ApiService инициализирован");
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка инициализации ApiService: {ex.Message}");
                MessageBox.Show($"Ошибка инициализации приложения: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown(1);
            }
        }


        private void RestoreSession()
        {
            try
            {
                var token = Settings.Default.AuthToken;
                var expiry = Settings.Default.TokenExpiry;
                var user = Settings.Default.CurrentUser;
                var role = Settings.Default.UserRole;

                if (!string.IsNullOrEmpty(token) && expiry > DateTime.UtcNow)
                {
                    // Восстанавливаем токен в ApiService
                    ApiService.SetToken(token);
                    CurrentUser = user;
                    UserRole = role;
                    TokenExpiry = expiry;

                    LogMessage($"Сессия восстановлена для пользователя: {CurrentUser}");

                    // Проверяем токен
                    TestTokenValidity();
                }
                else
                {
                    LogMessage("Сохраненной сессии нет или она истекла");
                    if (!string.IsNullOrEmpty(token) && expiry <= DateTime.UtcNow)
                    {
                        // Токен истек, очищаем
                        ClearSession();
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка восстановления сессии: {ex.Message}");
            }
        }

        private async void TestTokenValidity()
        {
            try
            {
                // Пробуем получить текущего пользователя для проверки токена
                var user = await ApiService.GetCurrentUserAsync();
                LogMessage($"Токен действителен. Пользователь: {user.Username}");
            }
            catch (Exception ex)
            {
                LogMessage($"Токен недействителен: {ex.Message}");
                ClearSession();
                ShowLoginWindow();
            }
        }

        private void OnSessionExpired()
        {
            // Вызывается когда ApiService обнаруживает 401 ошибку
            Dispatcher.Invoke(() =>
            {
                LogMessage("Сессия истекла (событие от ApiService)");
                ClearSession();
                ShowLoginWindow();
            });
        }

        private void ShowLoginIfNotAuthenticated()
        {
            if (!IsAuthenticated())
            {
                ShowLoginWindow();
            }
            else
            {
                ShowMainWindow();
            }
        }

        private void ShowLoginWindow()
        {
            var loginWindow = new LoginWindow();

            loginWindow.Closed += (s, e) =>
            {
                if (loginWindow.DialogResult == true)
                {
                    ShowMainWindow();
                }
                else
                {
                    if (!IsAuthenticated())
                    {
                        Shutdown();
                    }
                }
            };

            loginWindow.Show();

            if (MainWindow != null && MainWindow is MainWindow)
            {
                MainWindow.Close();
            }
            MainWindow = loginWindow;
        }

        private void ShowMainWindow()
        {
            var mainWindow = new MainWindow();
            mainWindow.Show();

            if (MainWindow is LoginWindow)
            {
                MainWindow.Close();
            }
            MainWindow = mainWindow;
        }

        public static void SaveSession(string token, string username, string role)
        {
            try
            {
                Console.WriteLine($"[SaveSession] Сохранение сессии для пользователя: {username}");
                Console.WriteLine($"[SaveSession] Токен получен: {token?.Substring(0, Math.Min(20, token.Length))}...");

                Settings.Default.AuthToken = token;
                Settings.Default.CurrentUser = username;
                Settings.Default.UserRole = role;
                Settings.Default.TokenExpiry = DateTime.UtcNow.AddHours(1);
                Settings.Default.Save();

                Console.WriteLine($"[SaveSession] Настройки сохранены");

                // Устанавливаем токен в ApiService
                ApiService.SetToken(token);
                CurrentUser = username;
                UserRole = role;

                Console.WriteLine($"[SaveSession] Текущий пользователь установлен: {CurrentUser}");
                Console.WriteLine($"[SaveSession] Проверка авторизации: {IsAuthenticated()}");

                LogMessage($"Сессия сохранена для пользователя: {username}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SaveSession] Ошибка: {ex.Message}");
                LogMessage($"Ошибка сохранения сессии: {ex.Message}");
            }
        }

        public static void ClearSession()
        {
            try
            {
                Settings.Default.AuthToken = string.Empty;
                Settings.Default.CurrentUser = string.Empty;
                Settings.Default.UserRole = string.Empty;
                Settings.Default.TokenExpiry = DateTime.MinValue;
                Settings.Default.Save();

                ApiService.ClearToken();
                CurrentUser = null;
                UserRole = null;

                LogMessage("Сессия очищена");
            }
            catch (Exception ex)
            {
                LogMessage($"Ошибка очистки сессии: {ex.Message}");
            }
        }

        public static bool IsAuthenticated()
        {
            try
            {
                var expiry = Settings.Default.TokenExpiry;
                var hasToken = !string.IsNullOrEmpty(Settings.Default.AuthToken);
                var tokenValid = expiry > DateTime.UtcNow;
                var hasCurrentUser = !string.IsNullOrEmpty(CurrentUser);

                Console.WriteLine($"[IsAuthenticated] Проверка:");
                Console.WriteLine($"[IsAuthenticated] Есть токен в настройках: {hasToken}");
                Console.WriteLine($"[IsAuthenticated] Токен действителен: {tokenValid} (истекает: {expiry})");
                Console.WriteLine($"[IsAuthenticated] Текущий пользователь: {CurrentUser}");
                Console.WriteLine($"[IsAuthenticated] Результат: {hasToken && tokenValid && hasCurrentUser}");

                return hasToken && tokenValid && hasCurrentUser;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[IsAuthenticated] Ошибка: {ex.Message}");
                return false;
            }
        }

        public static void LogMessage(string message)
        {
            Current.Dispatcher.Invoke(() =>
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