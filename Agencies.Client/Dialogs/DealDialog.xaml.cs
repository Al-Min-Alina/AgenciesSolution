using Agencies.Client.Services;
using Agencies.Client.ViewModels;
using Agencies.Core.DTO;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Agencies.Client.Dialogs
{
    public partial class DealDialog : Window
    {
        private readonly ApiService _apiService;
        private readonly UserDto _currentUser;
        private readonly bool _isEditMode;
        private readonly DealViewModel _viewModel;

        public DealDto Deal => _viewModel.Deal;
        public bool IsAdmin { get; set; }

        public DealDialog(ApiService apiService, DealDto deal = null)
        {
            InitializeComponent();
            _apiService = apiService;
            _isEditMode = deal != null;

            _viewModel = new DealViewModel
            {
                Deal = deal ?? new DealDto
                {
                    DealDate = DateTime.Now,
                    Status = "В ожидании"
                },
                WindowTitle = _isEditMode ? "Редактирование сделки" : "Новая сделка",
                Statuses = new ObservableCollection<string>
                {
                    "В ожидании",
                    "Завершено",
                    "Отменено"
                }
            };

            DataContext = _viewModel;

            Loaded += async (s, e) => await InitializeAsync();

            // Назначаем обработчики событий для обновления информации
            cbProperty.SelectionChanged += CbProperty_SelectionChanged;
            cbClient.SelectionChanged += CbClient_SelectionChanged;

            // Обновляем CanSave при изменении свойств
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_viewModel.PropertyId) ||
                    e.PropertyName == nameof(_viewModel.DealAmount) ||
                    e.PropertyName == nameof(_viewModel.ClientId) ||
                    e.PropertyName == nameof(_viewModel.Status))
                {
                    // Обновляем CanSave через вызов PropertyChanged
                    _viewModel.RaisePropertyChanged(nameof(_viewModel.CanSave));
                }
            };
        }

        private void CbProperty_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel?.Properties == null) return;

            var property = cbProperty.SelectedItem as PropertyDto;
            if (property != null)
            {
                // Обновляем информацию в текстовом блоке напрямую
                tbPropertyInfo.Text = $"{property.Title}\nАдрес: {property.Address}\nЦена: {property.Price:C}\nПлощадь: {property.Area} м²";

                // Обновляем ID в ViewModel
                _viewModel.PropertyId = property.Id;
            }
            else
            {
                tbPropertyInfo.Text = "Не выбран";
            }
        }

        private void CbClient_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_viewModel?.Clients == null) return;

            var client = cbClient.SelectedItem as ClientDto;
            if (client != null)
            {
                // Обновляем информацию в текстовом блоке напрямую
                tbClientInfo.Text = $"{client.FirstName} {client.LastName}\nТелефон: {client.Phone}\nEmail: {client.Email}\nБюджет: {client.Budget:C}";

                // Обновляем ID в ViewModel
                _viewModel.ClientId = client.Id;
            }
            else
            {
                tbClientInfo.Text = "Не выбран";
            }
        }

        private async Task InitializeAsync()
        {
            try
            {
                // Загружаем данные асинхронно
                await Task.Run(async () =>
                {
                    // Загружаем объекты недвижимости
                    var properties = await _apiService.GetPropertiesAsync();
                    var availableProperties = properties.Where(p => p.IsAvailable == true).ToList();
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _viewModel.Properties = new ObservableCollection<PropertyDto>(availableProperties);

                        // Устанавливаем выбранный объект, если он доступен
                        if (_viewModel.Deal.PropertyId > 0)
                        {
                            var selectedProperty = availableProperties.FirstOrDefault(p => p.Id == _viewModel.Deal.PropertyId);
                            if (selectedProperty != null)
                            {
                                cbProperty.SelectedValue = _viewModel.Deal.PropertyId;
                            }
                            else
                            {
                                // Если объект недоступен, сбрасываем выбор
                                _viewModel.PropertyId = 0;
                                _viewModel.Deal.PropertyId = 0;
                            }
                        }
                    });

                    // Загружаем клиентов
                    var clients = await _apiService.GetClientsAsync();
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _viewModel.Clients = new ObservableCollection<ClientDto>(clients);
                    });

                    // Загружаем агентов если пользователь - админ
                    if (IsAdmin)
                    {
                        var agents = await LoadAgentsAsync();
                        await Dispatcher.InvokeAsync(() =>
                        {
                            _viewModel.Agents = new ObservableCollection<UserDto>(agents);
                            cbAgent.Visibility = Visibility.Visible;
                            tbAgentLabel.Visibility = Visibility.Visible;
                        });
                    }
                });
            }
            catch (System.Exception ex)
            {
                _viewModel.ErrorMessage = $"Ошибка загрузки данных: {ex.Message}";
                _viewModel.HasError = true;
            }
        }

        private async Task<UserDto[]> LoadAgentsAsync()
        {
            try
            {
                // Загружаем агентов с сервера через ApiService
                var agents = await _apiService.GetAgentsAsync();
                Console.WriteLine($"API вернул {agents?.Count ?? 0} агентов");

                await Dispatcher.InvokeAsync(() =>
                {
                    if (agents != null && agents.Any())
                    {
                        Console.WriteLine($"Успешно загружено {agents.Count} агентов");

                        // Устанавливаем список агентов во ViewModel
                        _viewModel.Agents = new ObservableCollection<UserDto>(agents);

                        // Настраиваем ComboBox
                        if (cbAgent != null)
                        {
                            cbAgent.ItemsSource = _viewModel.Agents;
                            cbAgent.DisplayMemberPath = "Username";
                            cbAgent.SelectedValuePath = "Id";

                            // Выбор агента по умолчанию
                            if (_viewModel.Deal.AgentId > 0)
                            {
                                cbAgent.SelectedValue = _viewModel.Deal.AgentId;
                                Console.WriteLine($"Выбран существующий агент с ID: {_viewModel.Deal.AgentId}");
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

                        // Показываем элементы выбора агента
                        if (cbAgent != null) cbAgent.Visibility = Visibility.Visible;
                        if (tbAgentLabel != null) tbAgentLabel.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        Console.WriteLine($"Агенты не загружены или список пуст");

                        // Нет агентов или ошибка
                        if (_currentUser?.Role == "User")
                        {
                            // Агент назначает сделку себе
                            _viewModel.Deal.AgentId = _currentUser.Id;

                            if (cbAgent != null) cbAgent.Visibility = Visibility.Collapsed;
                            if (tbAgentLabel != null) tbAgentLabel.Visibility = Visibility.Collapsed;

                            Console.WriteLine($"Агент назначен на текущего пользователя: {_currentUser.Username}");
                        }
                        else
                        {
                            _viewModel.ErrorMessage = "Нет доступных агентов в системе";
                            _viewModel.HasError = true;

                            if (cbAgent != null) cbAgent.Visibility = Visibility.Collapsed;
                            if (tbAgentLabel != null) tbAgentLabel.Visibility = Visibility.Collapsed;
                        }
                    }
                });

                return agents?.ToArray() ?? Array.Empty<UserDto>();
            }
            catch (System.Net.Http.HttpRequestException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    Console.WriteLine($"Доступ запрещен (403): {httpEx.Message}");

                    if (_currentUser?.Role == "User")
                    {
                        // Используем только текущего пользователя как агента
                        _viewModel.Agents = new ObservableCollection<UserDto>
                        {
                            new UserDto { Id = _currentUser.Id, Username = _currentUser.Username, Role = _currentUser.Role }
                        };
                        _viewModel.Deal.AgentId = _currentUser.Id;

                        // Скрываем выбор агента
                        if (cbAgent != null) cbAgent.Visibility = Visibility.Collapsed;
                        if (tbAgentLabel != null) tbAgentLabel.Visibility = Visibility.Collapsed;

                        Console.WriteLine($"Агент назначен на текущего пользователя: {_currentUser.Username}");
                    }
                });

                return Array.Empty<UserDto>();
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    Console.WriteLine($"Ошибка загрузки агентов: {ex}");
                    _viewModel.ErrorMessage = $"Не удалось загрузить список агентов: {ex.Message}";
                    _viewModel.HasError = true;

                    if (cbAgent != null) cbAgent.Visibility = Visibility.Collapsed;
                    if (tbAgentLabel != null) tbAgentLabel.Visibility = Visibility.Collapsed;
                });

                return Array.Empty<UserDto>();
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

            if (_viewModel.PropertyId <= 0)
            {
                _viewModel.ErrorMessage = "Выберите объект недвижимости";
                _viewModel.HasError = true;
                cbProperty.Focus();
                return false;
            }

            if (_viewModel.ClientId <= 0)
            {
                _viewModel.ErrorMessage = "Выберите клиента";
                _viewModel.HasError = true;
                cbClient.Focus();
                return false;
            }

            if (_viewModel.DealAmount <= 0)
            {
                _viewModel.ErrorMessage = "Сумма сделки должна быть больше 0";
                _viewModel.HasError = true;
                txtDealAmount.Focus();
                return false;
            }

            if (_viewModel.Deal.DealDate == DateTime.MinValue)
            {
                _viewModel.ErrorMessage = "Укажите дату сделки";
                _viewModel.HasError = true;
                dpDealDate.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(_viewModel.Status))
            {
                _viewModel.ErrorMessage = "Выберите статус сделки";
                _viewModel.HasError = true;
                cbStatus.Focus();
                return false;
            }

            return true;
        }
    }

    public class DealViewModel : BaseViewModel
    {
        private DealDto _deal;
        public DealDto Deal
        {
            get => _deal;
            set => SetProperty(ref _deal, value);
        }

        // Для работы с Budget клиента (double? -> string)
        //public string BudgetDisplay
        //{
        //    get
        //    {
        //        if (_selectedClient != null && _selectedClient.Budget.HasValue)
        //            return _selectedClient.Budget.Value.ToString("N2");
        //        return "0.00";
        //    }
        //}

        //private ClientDto _selectedClient;
        //public ClientDto SelectedClient
        //{
        //    get => _selectedClient;
        //    set
        //    {
        //        SetProperty(ref _selectedClient, value);
        //        OnPropertyChanged(nameof(BudgetDisplay));
        //    }
        //}

        public double DealAmount
        {
            get => Deal?.DealAmount ?? 0.0;
            set
            {
                if (Deal != null && Deal.DealAmount != value)
                {
                    Deal.DealAmount = value;
                    OnPropertyChanged(nameof(DealAmount));
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        public int PropertyId
        {
            get => Deal?.PropertyId ?? 0;
            set
            {
                if (Deal != null && Deal.PropertyId != value)
                {
                    Deal.PropertyId = value;
                    OnPropertyChanged(nameof(PropertyId));
                    OnPropertyChanged(nameof(SelectedPropertyInfo));
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        public int ClientId
        {
            get => Deal?.ClientId ?? 0;
            set
            {
                if (Deal != null && Deal.ClientId != value)
                {
                    Deal.ClientId = value;
                    OnPropertyChanged(nameof(ClientId));
                    OnPropertyChanged(nameof(SelectedClientInfo));
                    OnPropertyChanged(nameof(CanSave));

                    //// Обновляем выбранного клиента
                    //if (Clients != null)
                    //    SelectedClient = Clients.FirstOrDefault(c => c.Id == value);
                }
            }
        }

        public string Status
        {
            get => Deal?.Status ?? "";
            set
            {
                if (Deal != null && Deal.Status != value)
                {
                    Deal.Status = value;
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(CanSave));
                }
            }
        }

        private ObservableCollection<PropertyDto> _properties;
        public ObservableCollection<PropertyDto> Properties
        {
            get => _properties;
            set
            {
                if (SetProperty(ref _properties, value))
                {
                    OnPropertyChanged(nameof(SelectedPropertyInfo));
                }
            }
        }

        private ObservableCollection<ClientDto> _clients;
        public ObservableCollection<ClientDto> Clients
        {
            get => _clients;
            set
            {
                if (SetProperty(ref _clients, value))
                {
                    OnPropertyChanged(nameof(SelectedClientInfo));
                }
            }
        }

        private ObservableCollection<UserDto> _agents;
        public ObservableCollection<UserDto> Agents
        {
            get => _agents;
            set => SetProperty(ref _agents, value);
        }

        private ObservableCollection<string> _statuses;
        public ObservableCollection<string> Statuses
        {
            get => _statuses;
            set => SetProperty(ref _statuses, value);
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

        public string SelectedPropertyInfo
        {
            get
            {
                if (PropertyId <= 0 || Properties == null)
                    return "Не выбран";

                var property = Properties.FirstOrDefault(p => p.Id == PropertyId);
                return property != null
                    ? $"{property.Title}\nАдрес: {property.Address}\nЦена: ${property.Price:N2}\nПлощадь: {property.Area} м²"
                    : "Не выбран";
            }
        }

        public string SelectedClientInfo
        {
            get
            {
                if (ClientId <= 0 || Clients == null)
                    return "Не выбран";

                var client = Clients.FirstOrDefault(c => c.Id == ClientId);
                if (client != null)
                {
                    // Форматируем бюджет в долларах
                    var budget = client.Budget.HasValue ? $"${client.Budget.Value:N2}" : "Не указан";
                    return $"{client.FirstName} {client.LastName}\nТелефон: {client.Phone}\nEmail: {client.Email}\nБюджет: {budget}";
                }
                return "Не выбран";
            }
        }

        public bool CanSave
        {
            get
            {
                Console.WriteLine($"=== Проверка CanSave для сделки ===");
                Console.WriteLine($"Deal is null: {Deal == null}");

                if (Deal != null)
                {
                    Console.WriteLine($"PropertyId: {PropertyId} (valid: {PropertyId > 0})");
                    Console.WriteLine($"ClientId: {ClientId} (valid: {ClientId > 0})");
                    Console.WriteLine($"DealAmount: {DealAmount} (valid: {DealAmount > 0})");
                    Console.WriteLine($"Status: '{Status}' (valid: {!string.IsNullOrWhiteSpace(Status)})");

                    bool result = PropertyId > 0 &&
                                 ClientId > 0 &&
                                 DealAmount > 0 &&
                                 !string.IsNullOrWhiteSpace(Status);

                    Console.WriteLine($"CanSave result: {result}");
                    Console.WriteLine($"=== End Check ===");

                    return result;
                }
                return false;
            }
        }
    }

    public class BaseViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void RaisePropertyChanged(string propertyName)
        {
            OnPropertyChanged(propertyName);
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}