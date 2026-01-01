using Agencies.Client.Services;
using Agencies.Client.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Agencies.Core.DTO;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;

namespace Agencies.Client.Dialogs
{
    public partial class ClientDialog : Window
    {
        private readonly ApiService _apiService;
        private readonly UserDto _currentUser;
        private readonly bool _isEditMode;
        private readonly ClientViewModel _viewModel;
        private readonly ValidationService _validationService;

        public ClientDto Client => _viewModel.Client;
        public bool IsAdmin => _viewModel.IsAdmin;

        public ClientDialog(ApiService apiService, UserDto currentUser, ClientDto client = null)
        {
            InitializeComponent();
            _apiService = apiService;
            _currentUser = currentUser;
            _validationService = new ValidationService();
            _isEditMode = client != null;

            _viewModel = new ClientViewModel
            {
                Client = client ?? new ClientDto(),
                WindowTitle = _isEditMode ? "Редактирование клиента" : "Новый клиент",
                IsAdmin = currentUser?.Role == "Admin"
            };

            DataContext = _viewModel;

            Loaded += async (s, e) => await InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                Console.WriteLine($"Инициализация диалога. Пользователь: {_currentUser?.Username}, Роль: {_currentUser?.Role}");

                // Загружаем список агентов если пользователь - админ
                if (_viewModel.IsAdmin)
                {
                    Console.WriteLine($"Пользователь админ, загружаем список агентов...");
                    await LoadAgentsAsync();
                }
                else if (_currentUser != null && _currentUser.Role == "User")
                {
                    // Для не-админов (агентов) устанавливаем себя как агента
                    _viewModel.Client.AgentId = _currentUser.Id;

                    // Скрываем элементы для выбора агента
                    cbAgent.Visibility = Visibility.Collapsed;
                    tbAgentLabel.Visibility = Visibility.Collapsed;

                    // Показываем информационное сообщение
                    if (tbAgentInfo != null)
                    {
                        tbAgentInfo.Text = $"Клиент будет закреплен за вами: {_currentUser.Username}";
                        tbAgentInfo.Visibility = Visibility.Visible;
                    }

                    Console.WriteLine($"Клиент закреплен за агентом: {_currentUser.Username}");
                }
                else
                {
                    // Если пользователь не админ и не агент, скрываем выбор агента
                    cbAgent.Visibility = Visibility.Collapsed;
                    tbAgentLabel.Visibility = Visibility.Collapsed;

                    if (tbAgentInfo != null)
                    {
                        tbAgentInfo.Visibility = Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка инициализации: {ex}");
                _viewModel.ErrorMessage = $"Ошибка загрузки данных: {ex.Message}";
                _viewModel.HasError = true;
            }
        }

        private async Task LoadAgentsAsync()
        {
            try
            {
                // Загружаем агентов с сервера через ApiService
                List<UserDto> agents = null;

                try
                {
                    agents = await _apiService.GetAgentsAsync();
                    Console.WriteLine($"API вернул {agents?.Count ?? 0} агентов");
                }
                catch (HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine($"Доступ запрещен (403): {httpEx.Message}");

                    if (_currentUser?.Role == "User")
                    {
                        // Используем только текущего пользователя как агента
                        agents = new List<UserDto> { _currentUser };
                        _viewModel.Client.AgentId = _currentUser.Id;

                        // Скрываем выбор агента, показываем информацию
                        cbAgent.Visibility = Visibility.Collapsed;
                        tbAgentLabel.Visibility = Visibility.Collapsed;

                        // Показываем информационное сообщение
                        if (tbAgentInfo != null)
                        {
                            tbAgentInfo.Text = $"Клиент будет закреплен за вами: {_currentUser.Username}";
                            tbAgentInfo.Visibility = Visibility.Visible;
                        }

                        Console.WriteLine($"Агент назначен на текущего пользователя: {_currentUser.Username}");
                        return;
                    }
                }

                if (agents != null && agents.Any())
                {
                    Console.WriteLine($"Успешно загружено {agents.Count} агентов");

                    // Устанавливаем список агентов во ViewModel
                    _viewModel.Agents = new ObservableCollection<UserDto>(agents);

                    // Настраиваем ComboBox
                    cbAgent.ItemsSource = _viewModel.Agents;
                    cbAgent.DisplayMemberPath = "Username";
                    cbAgent.SelectedValuePath = "Id";

                    // Показываем элементы выбора агента
                    cbAgent.Visibility = Visibility.Visible;
                    tbAgentLabel.Visibility = Visibility.Visible;

                    // Скрываем информационное сообщение
                    if (tbAgentInfo != null)
                    {
                        tbAgentInfo.Visibility = Visibility.Collapsed;
                    }

                    // Выбор агента по умолчанию
                    if (_viewModel.Client.AgentId > 0)
                    {
                        cbAgent.SelectedValue = _viewModel.Client.AgentId;
                        Console.WriteLine($"Выбран существующий агент с ID: {_viewModel.Client.AgentId}");
                    }
                    else if (_currentUser?.Role == "User" && agents.Any(a => a.Id == _currentUser.Id))
                    {
                        // Выбираем текущего пользователя-агента
                        cbAgent.SelectedValue = _currentUser.Id;
                        Console.WriteLine($"Автовыбор текущего агента: {_currentUser.Username}");
                    }
                    else if (agents.Any())
                    {
                        // Или первого агента
                        cbAgent.SelectedIndex = 0;
                        var firstAgent = agents.First();
                        Console.WriteLine($"Выбран первый агент: {firstAgent.Username}");
                    }
                }
                else
                {
                    Console.WriteLine($"Агенты не загружены или список пуст");

                    // Нет агентов или ошибка
                    if (_currentUser?.Role == "User")
                    {
                        // Агент назначает клиента себе
                        _viewModel.Client.AgentId = _currentUser.Id;
                        cbAgent.Visibility = Visibility.Collapsed;
                        tbAgentLabel.Visibility = Visibility.Collapsed;

                        // Показываем информационное сообщение
                        if (tbAgentInfo != null)
                        {
                            tbAgentInfo.Text = $"Клиент будет закреплен за вами";
                            tbAgentInfo.Visibility = Visibility.Visible;
                        }

                        Console.WriteLine($"Агент назначен на текущего пользователя: {_currentUser.Username}");
                    }
                    else
                    {
                        _viewModel.ErrorMessage = "Нет доступных агентов в системе";
                        _viewModel.HasError = true;
                        cbAgent.Visibility = Visibility.Collapsed;
                        tbAgentLabel.Visibility = Visibility.Collapsed;

                        if (tbAgentInfo != null)
                        {
                            tbAgentInfo.Visibility = Visibility.Collapsed;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка загрузки агентов: {ex}");
                _viewModel.ErrorMessage = $"Не удалось загрузить список агентов: {ex.Message}";
                _viewModel.HasError = true;
                cbAgent.Visibility = Visibility.Collapsed;
                tbAgentLabel.Visibility = Visibility.Collapsed;

                if (tbAgentInfo != null)
                {
                    tbAgentInfo.Visibility = Visibility.Collapsed;
                }
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

            // Если Budget должен оставаться double?
            double? budgetValue = _viewModel.Client.Budget ?? 0.0; 

            // Создаем DTO для валидации
            var clientForValidation = new CreateClientRequest
            {
                FirstName = _viewModel.Client.FirstName,
                LastName = _viewModel.Client.LastName,
                Phone = _viewModel.Client.Phone,
                Email = _viewModel.Client.Email,
                Budget = Convert.ToDouble(budgetValue.Value), 
                Requirements = _viewModel.Client.Requirements,
                AgentId = _viewModel.Client.AgentId ?? 0
            };

            // Используем метод валидации
            var validationResult = _validationService.ValidateClient(clientForValidation);

            if (!validationResult.IsValid)
            {
                _viewModel.ErrorMessage = validationResult.Summary;
                _viewModel.HasError = true;

                // Фокусируемся на первом поле с ошибкой
                if (validationResult.Errors.ContainsKey(nameof(CreateClientRequest.FirstName)))
                    txtFirstName.Focus();
                else if (validationResult.Errors.ContainsKey(nameof(CreateClientRequest.LastName)))
                    txtLastName.Focus();
                else if (validationResult.Errors.ContainsKey(nameof(CreateClientRequest.Email)))
                    txtEmail.Focus();
                else if (validationResult.Errors.ContainsKey(nameof(CreateClientRequest.Phone)))
                    txtPhone.Focus();
                else if (validationResult.Errors.ContainsKey(nameof(CreateClientRequest.Budget)))
                    txtBudget.Focus();

                return false;
            }

            return true;
        }
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
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

        private ObservableCollection<UserDto> _agents;
        public ObservableCollection<UserDto> Agents
        {
            get => _agents;
            set => SetProperty(ref _agents, value);
        }

        public bool IsAdmin { get; set; }

        public bool CanSave
        {
            get
            {
                Console.WriteLine($"Проверка CanSave: Client={Client != null}");
                if (Client != null)
                {
                    Console.WriteLine($"  FirstName='{Client.FirstName}' (пусто: {string.IsNullOrWhiteSpace(Client.FirstName)})");
                    Console.WriteLine($"  LastName='{Client.LastName}' (пусто: {string.IsNullOrWhiteSpace(Client.LastName)})");
                    Console.WriteLine($"  Email='{Client.Email}' (пусто: {string.IsNullOrWhiteSpace(Client.Email)})");
                    Console.WriteLine($"  Phone='{Client.Phone}' (пусто: {string.IsNullOrWhiteSpace(Client.Phone)})");
                }
                return true;
            }
        }
    }
}