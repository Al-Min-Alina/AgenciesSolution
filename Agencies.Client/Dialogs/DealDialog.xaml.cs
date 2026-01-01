using Agencies.Client.Services;
using Agencies.Client.ViewModels;
using Agencies.Core.DTO;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace Agencies.Client.Dialogs
{
    public partial class DealDialog : Window
    {
        private readonly ApiService _apiService;
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
                    Status = "Pending"
                },
                WindowTitle = _isEditMode ? "Редактирование сделки" : "Новая сделка",
                Statuses = new ObservableCollection<string>
                {
                    "Pending",
                    "Completed",
                    "Cancelled"
                }
            };

            DataContext = _viewModel;

            Loaded += async (s, e) => await InitializeAsync();
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
                    await Dispatcher.InvokeAsync(() =>
                    {
                        _viewModel.Properties = new ObservableCollection<PropertyDto>(properties);
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
            // В реальном приложении здесь запрос к API для получения списка агентов
            return await Task.Run(() =>
            {
                return new[]
                {
                    new UserDto { Id = 1, Username = "agent1", Role = "User" },
                    new UserDto { Id = 2, Username = "agent2", Role = "User" }
                };
            });
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

            if (_viewModel.Deal.PropertyId <= 0)
            {
                _viewModel.ErrorMessage = "Выберите объект недвижимости";
                _viewModel.HasError = true;
                cbProperty.Focus();
                return false;
            }

            if (_viewModel.Deal.ClientId <= 0)
            {
                _viewModel.ErrorMessage = "Выберите клиента";
                _viewModel.HasError = true;
                cbClient.Focus();
                return false;
            }

            if (_viewModel.Deal.DealAmount <= 0)
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

            if (string.IsNullOrWhiteSpace(_viewModel.Deal.Status))
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

        private ObservableCollection<PropertyDto> _properties;
        public ObservableCollection<PropertyDto> Properties
        {
            get => _properties;
            set => SetProperty(ref _properties, value);
        }

        private ObservableCollection<ClientDto> _clients;
        public ObservableCollection<ClientDto> Clients
        {
            get => _clients;
            set => SetProperty(ref _clients, value);
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
                if (Deal?.PropertyId == null || Properties == null)
                    return "Не выбран";

                var property = Properties.FirstOrDefault(p => p.Id == Deal.PropertyId);
                return property != null
                    ? $"{property.Title}\nАдрес: {property.Address}\nЦена: {property.Price:C}\nПлощадь: {property.Area} м²"
                    : "Не выбран";
            }
        }

        public string SelectedClientInfo
        {
            get
            {
                if (Deal?.ClientId == null || Clients == null)
                    return "Не выбран";

                var client = Clients.FirstOrDefault(c => c.Id == Deal.ClientId);
                return client != null
                    ? $"{client.FirstName} {client.LastName}\nТелефон: {client.Phone}\nEmail: {client.Email}\nБюджет: {client.Budget:C}"
                    : "Не выбран";
            }
        }

        public bool CanSave => Deal != null &&
                              Deal.PropertyId > 0 &&
                              Deal.ClientId > 0 &&
                              Deal.DealAmount > 0 &&
                              !string.IsNullOrWhiteSpace(Deal.Status);
    }
}