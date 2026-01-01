using Agencies.Client.Services;
using Agencies.Core.DTO;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace Agencies.Client
{
    public partial class LoginWindow : Window
    {
        private readonly ApiService _apiService;
        public LoginResponse CurrentUser { get; private set; }

        public LoginWindow(ApiService apiService)
        {
            InitializeComponent();
            _apiService = apiService;

            // Фокус на поле ввода при загрузке окна
            Loaded += (s, e) => txtUsername.Focus();
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            var username = txtUsername.Text.Trim();
            var password = txtPassword.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                ShowError("Введите имя пользователя и пароль");
                return;
            }

            btnLogin.IsEnabled = false;
            tbError.Visibility = Visibility.Collapsed;
            tbError.Text = string.Empty;

            try
            {
                var loginRequest = new LoginRequest
                {
                    Username = username,
                    Password = password
                };

                // Выполняем асинхронный запрос с таймаутом
                var response = await Task.Run(() => _apiService.LoginAsync(loginRequest));

                if (response != null && !string.IsNullOrEmpty(response.Token))
                {
                    CurrentUser = response;
                    _apiService.SetToken(response.Token);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowError("Неверное имя пользователя или пароль");
                    txtPassword.Focus();
                    txtPassword.SelectAll();
                }
            }
            catch (HttpRequestException httpEx)
            {
                // Ошибка HTTP подключения
                ShowError($"Ошибка подключения к серверу. Проверьте:\n" +
                         $"1. Запущен ли сервер API\n" +
                         $"2. Корректность адреса сервера\n" +
                         $"3. Сетевое подключение\n\n" +
                         $"Детали: {GetUserFriendlyErrorMessage(httpEx)}");
            }
            catch (TaskCanceledException)
            {
                ShowError("Превышено время ожидания ответа от сервера");
            }
            catch (Exception ex)
            {
                ShowError($"Ошибка авторизации: {GetUserFriendlyErrorMessage(ex)}");
            }
            finally
            {
                btnLogin.IsEnabled = true;
            }
        }

        private string GetUserFriendlyErrorMessage(Exception ex)
        {
            // Упрощаем сообщение об ошибке для пользователя
            var baseEx = ex.GetBaseException();

            if (baseEx is HttpRequestException httpEx)
            {
                if (httpEx.Message.Contains("No connection could be made"))
                    return "Не удалось установить соединение с сервером";

                if (httpEx.Message.Contains("Connection refused"))
                    return "Сервер отказал в подключении";

                if (httpEx.Message.Contains("timed out"))
                    return "Истекло время ожидания ответа";
            }

            return baseEx.Message;
        }

        private void ShowError(string message)
        {
            Dispatcher.Invoke(() =>
            {
                tbError.Text = message;
                tbError.Visibility = Visibility.Visible;

                // Автоматический фокус на поле пароля при ошибке
                txtPassword.Focus();
                txtPassword.SelectAll();
            });
        }

        private void TxtUsername_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            tbError.Visibility = Visibility.Collapsed;
        }

        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            tbError.Visibility = Visibility.Collapsed;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TxtUsername_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && !string.IsNullOrWhiteSpace(txtUsername.Text))
            {
                txtPassword.Focus();
            }
        }

        private void TxtPassword_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter && !string.IsNullOrWhiteSpace(txtPassword.Password))
            {
                BtnLogin_Click(sender, e);
            }
        }
    }
}