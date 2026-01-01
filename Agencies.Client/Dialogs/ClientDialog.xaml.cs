using Agencies.Client.Services;
using Agencies.Client.ViewModels;
using Agencies.Core.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Agencies.Client.Dialogs
{
    public partial class ClientDialog : Window
    {
        private readonly ApiService _apiService;
        private readonly bool _isEditMode;
        private readonly ClientViewModel _viewModel;

        public ClientDto Client => _viewModel.Client;
        public bool IsAdmin { get; set; }
        private readonly ValidationService _validationService;

        public ClientDialog(ApiService apiService, ClientDto client = null)
        {
            InitializeComponent();
            _apiService = apiService;
            _validationService = new ValidationService();
            _isEditMode = client != null;

            _viewModel = new ClientViewModel
            {
                Client = client ?? new ClientDto(),
                WindowTitle = _isEditMode ? "Редактирование клиента" : "Новый клиент",
                IsAdmin = false // Will be set from parent window
            };

            DataContext = _viewModel;

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Загружаем список агентов если пользователь - админ
                if (IsAdmin)
                {
                    var agents = await LoadAgentsAsync();
                    if (agents != null)
                    {
                        cbAgent.ItemsSource = agents;
                        cbAgent.Visibility = Visibility.Visible;
                        tbAgentLabel.Visibility = Visibility.Visible;

                        if (_viewModel.Client.AgentId == 0)
                        {
                            // Устанавливаем текущего пользователя по умолчанию
                            var currentUser = await GetCurrentUserAsync();
                            if (currentUser != null)
                            {
                                _viewModel.Client.AgentId = currentUser.UserId;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                _viewModel.ErrorMessage = $"Ошибка загрузки данных: {ex.Message}";
                _viewModel.HasError = true;
            }
        }

        private async Task<List<UserDto>> LoadAgentsAsync()
        {
            // В реальном приложении здесь был бы запрос к API для получения списка агентов
            // Для демонстрации возвращаем тестовые данные
            return await Task.Run(() =>
            {
                return new List<UserDto>
                {
                    new UserDto { Id = 1, Username = "agent1", Role = "User" },
                    new UserDto { Id = 2, Username = "agent2", Role = "User" }
                };
            });
        }

        private async Task<UserProfile> GetCurrentUserAsync()
        {
            try
            {
                // В реальном приложении получаем профиль из API
                // Пока возвращаем тестовые данные
                return await Task.Run(() => new UserProfile
                {
                    UserId = 1,
                    Username = "current_user",
                    Role = "User"
                });
            }
            catch
            {
                return null;
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (ValidateInput())
            {
                DialogResult = true;
                Close();
            }
        }

        private bool ValidateInput()
        {
            _viewModel.HasError = false;
            _viewModel.ErrorMessage = string.Empty;

            // Получаем Budget из ViewModel и преобразуем в decimal
            decimal budgetValue;
            if (_viewModel.Client.Budget.HasValue)
            {
                // Явное преобразование double? в decimal
                budgetValue = Convert.ToDecimal(_viewModel.Client.Budget.Value);
            }
            else
            {
                budgetValue = 0m; // Значение по умолчанию
            }

            // Создаем DTO для валидации
            var clientForValidation = new ClientValidationDto
            {
                FirstName = _viewModel.Client.FirstName,
                LastName = _viewModel.Client.LastName,
                Phone = _viewModel.Client.Phone,
                Email = _viewModel.Client.Email,
                Budget = budgetValue,
                Requirements = _viewModel.Client.Requirements
            };

            // Используем метод валидации
            var validationResult = _validationService.ValidateClient(clientForValidation);

            if (!validationResult.IsValid)
            {
                _viewModel.ErrorMessage = validationResult.Summary;
                _viewModel.HasError = true;

                // Фокусируемся на первом поле с ошибкой
                if (validationResult.Errors.ContainsKey(nameof(ClientValidationDto.FirstName)))
                    txtFirstName.Focus();
                else if (validationResult.Errors.ContainsKey(nameof(ClientValidationDto.LastName)))
                    txtLastName.Focus();
                else if (validationResult.Errors.ContainsKey(nameof(ClientValidationDto.Email)))
                    txtEmail.Focus();
                else if (validationResult.Errors.ContainsKey(nameof(ClientValidationDto.Phone)))
                    txtPhone.Focus();
                else if (validationResult.Errors.ContainsKey(nameof(ClientValidationDto.Budget)))
                    txtBudget.Focus();

                return false;
            }

            return true;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }
    }

    public class ClientViewModel : BaseViewModel
    {
        private ClientDto _client;
        public ClientDto Client
        {
            get => _client;
            set => SetProperty(ref _client, value);
        }

        private string _windowTitle;
        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            set => SetProperty(ref _hasError, value);
        }

        public bool IsAdmin { get; set; }

        public bool CanSave => !string.IsNullOrWhiteSpace(Client?.FirstName) &&
                              !string.IsNullOrWhiteSpace(Client?.LastName);
    }

    public class UserProfile
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Role { get; set; }
    }

    // Класс для валидации клиента
    public class ClientValidationDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Phone { get; set; }
        public string Email { get; set; }
        public decimal Budget { get; set; }
        public string Requirements { get; set; }
    }

    // Результат валидации
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Summary { get; set; }
        public Dictionary<string, string> Errors { get; set; } = new Dictionary<string, string>();
    }

    // Реализация ValidationService
    public class ValidationService
    {
        public ValidationResult ValidateClient(ClientValidationDto client)
        {
            var result = new ValidationResult();
            var errors = new Dictionary<string, string>();

            // Валидация имени
            if (string.IsNullOrWhiteSpace(client.FirstName))
            {
                errors[nameof(ClientValidationDto.FirstName)] = "Имя обязательно для заполнения";
            }
            else if (client.FirstName.Length > 50)
            {
                errors[nameof(ClientValidationDto.FirstName)] = "Имя не должно превышать 50 символов";
            }

            // Валидация фамилии
            if (string.IsNullOrWhiteSpace(client.LastName))
            {
                errors[nameof(ClientValidationDto.LastName)] = "Фамилия обязательна для заполнения";
            }
            else if (client.LastName.Length > 50)
            {
                errors[nameof(ClientValidationDto.LastName)] = "Фамилия не должна превышать 50 символов";
            }

            // Валидация email
            if (!string.IsNullOrWhiteSpace(client.Email))
            {
                if (!IsValidEmail(client.Email))
                {
                    errors[nameof(ClientValidationDto.Email)] = "Неверный формат email";
                }
            }

            // Валидация телефона
            if (!string.IsNullOrWhiteSpace(client.Phone))
            {
                if (!IsValidPhone(client.Phone))
                {
                    errors[nameof(ClientValidationDto.Phone)] = "Неверный формат телефона";
                }
            }

            // Валидация бюджета
            if (client.Budget < 0)
            {
                errors[nameof(ClientValidationDto.Budget)] = "Бюджет не может быть отрицательным";
            }

            result.IsValid = errors.Count == 0;
            result.Errors = errors;

            if (!result.IsValid)
            {
                result.Summary = string.Join("; ", errors.Values);
            }

            return result;
        }

        private bool IsValidEmail(string email)
        {
            try
            {
                var addr = new System.Net.Mail.MailAddress(email);
                return addr.Address == email;
            }
            catch
            {
                return false;
            }
        }

        private bool IsValidPhone(string phone)
        {
            // Простая валидация телефона
            return System.Text.RegularExpressions.Regex.IsMatch(phone, @"^[\d\s\-\+\(\)]{6,20}$");
        }
    }
}