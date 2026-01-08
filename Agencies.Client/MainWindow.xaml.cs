using Agencies.Client.Dialogs;
using Agencies.Client.Services;
using Agencies.Client.Views;
using Agencies.Core.DTO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Agencies.Client
{
    public partial class MainWindow : Window
    {
        private readonly ApiService _apiService;
        private readonly BackgroundDataLoader _dataLoader;
        private ObservableCollection<PropertyDto> _properties;
        private ObservableCollection<ClientDto> _clients;
        private ObservableCollection<DealDto> _deals;
        private LoginResponse _currentUser;
        private ProgressDialog _progressDialog;
        private CancellationTokenSource _refreshTokenSource;
        private bool _isAutoRefreshEnabled;
        private readonly DispatcherTimer _autoRefreshTimer;
        private bool _isLoading;
        private ObservableCollection<DealDto> _allDeals;

        public MainWindow()
        {
            InitializeComponent();

            // Инициализация сервиса и загрузчика данных
            _apiService = new ApiService();
            _dataLoader = new BackgroundDataLoader(_apiService, Dispatcher);

            // Инициализация коллекций
            _properties = new ObservableCollection<PropertyDto>();
            _clients = new ObservableCollection<ClientDto>();
            _deals = new ObservableCollection<DealDto>();
            _allDeals = new ObservableCollection<DealDto>();

            // Привязка данных
            dgProperties.ItemsSource = _properties;
            dgClients.ItemsSource = _clients;
            dgDeals.ItemsSource = _deals;

            dgProperties.SelectionChanged += (s, e) => UpdateEditButtons();
            dgClients.SelectionChanged += (s, e) => UpdateEditButtons();
            dgDeals.SelectionChanged += (s, e) => UpdateEditButtons();

            // Настройка таймера автообновления (каждые 5 минут)
            _autoRefreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            _autoRefreshTimer.Tick += async (s, e) => await AutoRefreshDataAsync();

            // Настройка событий загрузчика данных
            SetupDataLoaderEvents();
        }

        private void SetupDataLoaderEvents()
        {
            _dataLoader.DataLoaded += OnDataLoaded;
            _dataLoader.LoadingStatusChanged += OnLoadingStatusChanged;
            _dataLoader.LoadingError += OnLoadingError;
        }

        private void CheckLoadingComplete()
        {
            // Этот метод можно расширить для более точного отслеживания загрузки
            if (_isLoading)
            {
                _isLoading = false;
                UpdateUIForLoading(false); // Восстанавливаем кнопки
            }
        }

        private void OnDataLoaded(object sender, DataLoadedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                switch (e.DataType)
                {
                    case DataType.Properties:
                        _properties.Clear();
                        if (e.Data is List<PropertyDto> properties)
                        {
                            foreach (var property in properties)
                            {
                                _properties.Add(property);
                            }
                        }
                        break;

                    case DataType.Clients:
                        _clients.Clear();
                        if (e.Data is List<ClientDto> clients)
                        {
                            foreach (var client in clients)
                            {
                                _clients.Add(client);
                            }
                        }
                        break;

                    case DataType.Deals:
                        _deals.Clear();
                        _allDeals.Clear(); 
                        if (e.Data is List<DealDto> deals)
                        {
                            foreach (var deal in deals)
                            {
                                _deals.Add(deal);
                                _allDeals.Add(deal); 
                            }
                        }
                        break;
                }

                // Проверяем, завершена ли загрузка всех данных
                CheckLoadingComplete();

                // Обновляем статус
                tbStatus.Text = $"Загружено: {_properties.Count} объектов, {_clients.Count} клиентов, {_deals.Count} сделок";
                tbConnectionStatus.Text = "Подключено";

                // ВОССТАНАВЛИВАЕМ кнопки редактирования
                UpdateEditButtons(); // Добавить эту строку
            });
        }

        private void OnLoadingStatusChanged(object sender, string status)
        {
            Dispatcher.Invoke(() =>
            {
                tbStatus.Text = status;
            });
        }

        private void OnLoadingError(object sender, Exception ex)
        {
            Dispatcher.Invoke(() =>
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                tbStatus.Text = "Ошибка загрузки";

                // Сбрасываем состояние загрузки при ошибке
                _isLoading = false;
                UpdateUIForLoading(false);
            });
        }

        private async void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            var loginWindow = new LoginWindow(_apiService);
            if (loginWindow.ShowDialog() == true)
            {
                _currentUser = loginWindow.CurrentUser;
                UpdateUIForUser();

                // Запускаем загрузку данных через BackgroundDataLoader
                ShowProgressDialog("Загрузка данных", "Инициализация системы...");
                UpdateProgress(10, "Подключение к серверу...");

                await _dataLoader.LoadAllDataAsync();

                UpdateProgress(100, "Загрузка завершена");
                HideProgressDialog();

                UpdateEditButtons();
                
                // Запускаем автообновление, если пользователь админ
                if (_currentUser.Role == "Admin")
                {
                    EnableAutoRefresh(true);
                    // Включаем чекбоксы автообновления для каждого раздела
                    cbAutoRefreshProperties.IsChecked = true;
                    cbAutoRefreshClients.IsChecked = true;
                    cbAutoRefreshDeals.IsChecked = true;
                }
            }
        }

        private void BtnLogout_Click(object sender, RoutedEventArgs e)
        {
            _currentUser = null;
            _apiService.ClearToken();
            UpdateUIForUser();
            ClearData();
            EnableAutoRefresh(false);

            // Отключаем чекбоксы автообновления
            cbAutoRefreshProperties.IsChecked = false;
            cbAutoRefreshClients.IsChecked = false;
            cbAutoRefreshDeals.IsChecked = false;
        }

        private void UpdateUIForUser()
        {
            Dispatcher.Invoke(() =>
            {
                if (_currentUser != null)
                {
                    tbUsername.Text = $"{_currentUser.Username} ({_currentUser.Role})";
                    btnLogin.IsEnabled = false;
                    btnLogout.IsEnabled = true;

                    // Показываем/скрываем функционал в зависимости от роли
                    tabReports.IsEnabled = _currentUser.Role == "Admin";

                    // Включаем чекбоксы автообновления только для админов
                    cbAutoRefreshProperties.IsEnabled = _currentUser.Role == "Admin";
                    cbAutoRefreshClients.IsEnabled = _currentUser.Role == "Admin";
                    cbAutoRefreshDeals.IsEnabled = _currentUser.Role == "Admin";

                    // ВЫЗЫВАЕМ UpdateEditButtons чтобы обновить все кнопки редактирования
                    UpdateEditButtons();
                }
                else
                {
                    tbUsername.Text = "Не авторизован";
                    btnLogin.IsEnabled = true;
                    btnLogout.IsEnabled = false;

                    // Блокируем функционал
                    tabReports.IsEnabled = false;

                    // Отключаем все кнопки редактирования
                    btnAddProperty.IsEnabled = false;
                    btnEditProperty.IsEnabled = false;
                    btnDeleteProperty.IsEnabled = false;

                    btnAddClient.IsEnabled = false;
                    btnEditClient.IsEnabled = false;
                    btnDeleteClient.IsEnabled = false;

                    btnAddDeal.IsEnabled = false;
                    btnEditDeal.IsEnabled = false;
                    btnDeleteDeal.IsEnabled = false;

                    // Отключаем чекбоксы автообновления
                    cbAutoRefreshProperties.IsEnabled = false;
                    cbAutoRefreshClients.IsEnabled = false;
                    cbAutoRefreshDeals.IsEnabled = false;
                }
            });
        }

        private void ClearData()
        {
            _properties.Clear();
            _clients.Clear();
            _deals.Clear();
            tbStatus.Text = "Готов";
            tbConnectionStatus.Text = "Не подключено";
        }

        private async void BtnRefreshProperties_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
            {
                MessageBox.Show("Пожалуйста, авторизуйтесь", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await StartLoadingAsync("Загрузка объектов недвижимости...");
        }

        private async void BtnAddProperty_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser?.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут добавлять объекты", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new PropertyDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ShowProgressDialog("Создание объекта", "Пожалуйста, подождите...");

                    // Используем Task для асинхронного создания
                    var newProperty = await Task.Run(async () =>
                    {
                        return await _apiService.CreatePropertyAsync(dialog.Property);
                    });

                    await Dispatcher.InvokeAsync(() =>
                    {
                        _properties.Add(newProperty);
                        tbStatus.Text = "Объект успешно добавлен";
                    });

                    // Обновляем кеш
                    _dataLoader.ClearCache();
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Ошибка создания: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    HideProgressDialog();
                }
            }
        }

        private async void BtnEditProperty_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser?.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут редактировать объекты", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgProperties.SelectedItem is PropertyDto selectedProperty)
            {
                // Открываем диалог
                var dialog = new PropertyDialog(selectedProperty.Id, selectedProperty);

                // Просто открываем диалог - он сам сохранит данные
                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    // Диалог успешно сохранил данные на сервере
                    // Обновляем локальные данные

                    try
                    {
                        // Обновляем список объектов
                        await _dataLoader.LoadPropertiesAsync(true); // force refresh
                        tbStatus.Text = "Объект успешно обновлен";
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Ошибка обновления списка: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите объект для редактирования", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnDeleteProperty_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser?.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут удалять объекты", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgProperties.SelectedItem is PropertyDto selectedProperty)
            {
                var result = MessageBox.Show($"Удалить объект '{selectedProperty.Title}'?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        ShowProgressDialog("Удаление объекта", "Удаление...");

                        var success = await Task.Run(async () =>
                        {
                            return await _apiService.DeletePropertyAsync(selectedProperty.Id);
                        });

                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (success)
                            {
                                _properties.Remove(selectedProperty);
                                tbStatus.Text = "Объект успешно удален";
                            }
                        });

                        // Обновляем кеш
                        _dataLoader.ClearCache();
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    finally
                    {
                        HideProgressDialog();
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите объект для удаления", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TxtSearchProperty_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = txtSearchProperty.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                dgProperties.ItemsSource = _properties;
            }
            else
            {
                var filtered = _properties.Where(p =>
                    p.Title.ToLower().Contains(searchText) ||
                    p.Address.ToLower().Contains(searchText) ||
                    p.Description.ToLower().Contains(searchText) ||
                    p.Type.ToLower().Contains(searchText));

                dgProperties.ItemsSource = filtered;
            }
        }

        private async void BtnRefreshClients_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
            {
                MessageBox.Show("Пожалуйста, авторизуйтесь", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await StartLoadingAsync("Загрузка клиентов...");
        }

        private async void BtnAddClient_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
            {
                MessageBox.Show("Пожалуйста, авторизуйтесь", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var userDto = new UserDto
            {
                Id = _currentUser.Id,
                Username = _currentUser.Username,
                Role = _currentUser.Role
            };

            var dialog = new ClientDialog(_apiService, userDto)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ShowProgressDialog("Создание клиента", "Пожалуйста, подождите...");

                    // Используем Task для асинхронного создания
                    var newClient = await Task.Run(async () =>
                    {
                        return await _apiService.CreateClientAsync(new CreateClientRequest
                        {
                            FirstName = dialog.Client.FirstName,
                            LastName = dialog.Client.LastName,
                            Phone = dialog.Client.Phone,
                            Email = dialog.Client.Email,
                            Requirements = dialog.Client.Requirements,
                            Budget = dialog.Client.Budget,
                            AgentId = _currentUser.Role == "Admin" ? dialog.Client.AgentId : null
                        });
                    });

                    await Dispatcher.InvokeAsync(() =>
                    {
                        _clients.Add(newClient);
                        tbStatus.Text = "Клиент успешно добавлен";
                    });

                    // Обновляем кеш
                    _dataLoader.ClearCache();
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show($"Ошибка создания: {ex.Message}", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                }
                finally
                {
                    HideProgressDialog();
                }
            }
        }

        private async void BtnEditClient_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
            {
                MessageBox.Show("Пожалуйста, авторизуйтесь", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

                if (dgClients.SelectedItem is ClientDto selectedClient)
                {
                bool canEdit = false;

                if (_currentUser.Role == "Admin")
                {
                    canEdit = true; // Админы могут редактировать всех
                }
                else
                {
                    // Для НЕ-админов проверяем 3 условия:

                    // 1. Проверка по AgentId (самая надежная)
                    if (selectedClient.AgentId.HasValue && selectedClient.AgentId.Value == _currentUser.Id)
                    {
                        canEdit = true;
                    }
                    // 2. Проверка по AgentName (если ID не совпадает)
                    else if (!string.IsNullOrEmpty(selectedClient.AgentName))
                    {
                        string currentUsername = _currentUser.Username.ToLower().Trim();
                        string clientAgentName = selectedClient.AgentName.ToLower().Trim();

                        // Сравниваем имена (может быть "Anna", "anna", "Anna (ID: 1)" и т.д.)
                        if (clientAgentName.Contains(currentUsername))
                        {
                            canEdit = true;
                        }
                    }
                    // 3. Если у клиента вообще нет агента, доступ запрещен для не-админов
                    else if (!selectedClient.AgentId.HasValue)
                    {
                        MessageBox.Show("Этот клиент не назначен агенту. Только администратор может редактировать.",
                            "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (!canEdit)
                {
                    // Детальное сообщение для отладки
                    string debugInfo = $"Ваш ID: {_currentUser.Id}, Имя: {_currentUser.Username}, Роль: {_currentUser.Role}\n" +
                                     $"ID агента клиента: {selectedClient.AgentId}, Имя агента: {selectedClient.AgentName ?? "Не указано"}";

                    MessageBox.Show($"Вы можете редактировать только своих клиентов.\n\n{debugInfo}",
                        "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Создаем UserDto из LoginResponse
                var userDto = new UserDto
                    {
                        Id = _currentUser.Id,
                        Username = _currentUser.Username,
                        Role = _currentUser.Role
                    };

                    // Теперь правильно: ApiService, UserDto, ClientDto (selectedClient для редактирования)
                    var dialog = new ClientDialog(_apiService, userDto, selectedClient)
                    {
                        Owner = this
                    };

                    if (dialog.ShowDialog() == true)
                    {
                        try
                        {
                            ShowProgressDialog("Обновление клиента", "Сохранение изменений...");

                            var updatedClient = await Task.Run(async () =>
                            {
                                return await _apiService.UpdateClientAsync(
                                    selectedClient.Id,
                                    new UpdateClientRequest
                                    {
                                        FirstName = dialog.Client.FirstName,
                                        LastName = dialog.Client.LastName,
                                        Phone = dialog.Client.Phone,
                                        Email = dialog.Client.Email,
                                        Requirements = dialog.Client.Requirements,
                                        Budget = dialog.Client.Budget,
                                        AgentId = _currentUser.Role == "Admin" ? dialog.Client.AgentId : null
                                    });
                            });

                        await Dispatcher.InvokeAsync(() =>
                        {
                            // Находим индекс клиента по ID (а не по ссылке на объект)
                            var index = -1;
                            for (int i = 0; i < _clients.Count; i++)
                            {
                                if (_clients[i].Id == selectedClient.Id) // Сравниваем по ID
                                {
                                    index = i;
                                    break;
                                }
                            }

                            if (index >= 0)
                            {
                                // Заменяем объект целиком
                                _clients[index] = updatedClient;

                                tbStatus.Text = "Клиент успешно обновлен";
                                Console.WriteLine($"Клиент обновлен в списке на позиции {index}");
                            }
                            else
                            {
                                // Если клиент не найден (маловероятно), добавляем новый
                                _clients.Add(updatedClient);
                                tbStatus.Text = "Клиент добавлен";
                                Console.WriteLine("Клиент не найден в списке, добавлен новый");
                            }
                        });

                        // Обновляем кеш
                        _dataLoader.ClearCache();
                        }
                        catch (Exception ex)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка",
                                    MessageBoxButton.OK, MessageBoxImage.Error);
                            });
                        }
                        finally
                        {
                            HideProgressDialog();
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Выберите клиента для редактирования", "Внимание",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        

        private async void BtnDeleteClient_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser?.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут удалять клиентов", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgClients.SelectedItem is ClientDto selectedClient)
            {
                var result = MessageBox.Show($"Удалить клиента '{selectedClient.FirstName} {selectedClient.LastName}'?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        ShowProgressDialog("Удаление клиента", "Удаление...");

                        var success = await Task.Run(async () =>
                        {
                            return await _apiService.DeleteClientAsync(selectedClient.Id);
                        });

                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (success)
                            {
                                _clients.Remove(selectedClient);
                                tbStatus.Text = "Клиент успешно удален";
                            }
                        });

                        // Обновляем кеш
                        _dataLoader.ClearCache();
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    finally
                    {
                        HideProgressDialog();
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите клиента для удаления", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TxtSearchClient_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = txtSearchClient.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchText))
            {
                dgClients.ItemsSource = _clients;
            }
            else
            {
                var filtered = _clients.Where(c =>
                    c.FirstName.ToLower().Contains(searchText) ||
                    c.LastName.ToLower().Contains(searchText) ||
                    (c.Email?.ToLower().Contains(searchText) ?? false) ||
                    (c.Phone?.Contains(searchText) ?? false) ||
                    (c.Requirements?.ToLower().Contains(searchText) ?? false));

                dgClients.ItemsSource = filtered;
            }
        }

        private async void BtnRefreshDeals_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
            {
                MessageBox.Show("Пожалуйста, авторизуйтесь", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            await StartLoadingAsync("Загрузка сделок...");
        }

        private async void BtnAddDeal_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
            {
                MessageBox.Show("Пожалуйста, авторизуйтесь", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var dialog = new DealDialog(_apiService)
            {
                IsAdmin = _currentUser.Role == "Admin",
                Owner = this
            };

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    ShowProgressDialog("Создание сделки", "Создание сделки...");

                    var newDeal = await _apiService.CreateDealAsync(new CreateDealRequest
                    {
                        PropertyId = dialog.Deal.PropertyId,
                        ClientId = dialog.Deal.ClientId,
                        DealAmount = dialog.Deal.DealAmount,
                        DealDate = dialog.Deal.DealDate,
                        Status = dialog.Deal.Status,
                        AgentId = _currentUser.Role == "Admin" ? dialog.Deal.AgentId : 0
                    });

                    _deals.Add(newDeal);
                    tbStatus.Text = "Сделка успешно создана";

                    // Обновляем кеш
                    _dataLoader.ClearCache();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Ошибка создания: {ex.Message}", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    HideProgressDialog();
                }
            }
        }

        private async void BtnEditDeal_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser == null)
            {
                MessageBox.Show("Пожалуйста, авторизуйтесь", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgDeals.SelectedItem is DealDto selectedDeal)
            {
                bool canEdit = false;

                if (_currentUser.Role == "Admin")
                {
                    canEdit = true;
                }
                else
                {
                    if (selectedDeal.AgentId == _currentUser.Id)
                    {
                        canEdit = true;
                    }
                    else if (!string.IsNullOrEmpty(selectedDeal.AgentName))
                    {
                        string currentUsername = _currentUser.Username.ToLower().Trim();
                        string dealAgentName = selectedDeal.AgentName.ToLower().Trim();

                        if (dealAgentName.Contains(currentUsername))
                        {
                            canEdit = true;
                        }
                    }
                }

                if (!canEdit)
                {
                    string debugInfo = $"Ваш ID: {_currentUser.Id}, Имя: {_currentUser.Username}, Роль: {_currentUser.Role}\n" +
                                     $"ID агента сделки: {selectedDeal.AgentId}, Имя агента: {selectedDeal.AgentName ?? "Не указано"}";

                    MessageBox.Show($"Вы можете редактировать только свои сделки.\n\n{debugInfo}",
                        "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new DealDialog(_apiService, selectedDeal)
                {
                    IsAdmin = _currentUser.Role == "Admin",
                    Owner = this
                };

                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        ShowProgressDialog("Обновление сделки", "Сохранение изменений...");

                        // Теперь UpdateDealAsync всегда должен возвращать DealDto
                        var updatedDeal = await Task.Run(async () =>
                        {
                            return await _apiService.UpdateDealAsync(
                                selectedDeal.Id,
                                new UpdateDealRequest
                                {
                                    PropertyId = dialog.Deal.PropertyId,
                                    ClientId = dialog.Deal.ClientId,
                                    DealAmount = dialog.Deal.DealAmount,
                                    DealDate = dialog.Deal.DealDate,
                                    Status = dialog.Deal.Status,
                                    AgentId = _currentUser.Role == "Admin" ? dialog.Deal.AgentId : 0
                                });
                        });

                        await Dispatcher.InvokeAsync(() =>
                        {
                            // Теперь updatedDeal не должен быть null
                            if (updatedDeal != null)
                            {
                                // Обновляем элемент в коллекции
                                var index = _deals.IndexOf(selectedDeal);
                                if (index >= 0)
                                {
                                    _deals[index] = updatedDeal;
                                    tbStatus.Text = "Сделка успешно обновлена";
                                }
                                else
                                {
                                    // Если сделка не найдена, добавляем её
                                    _deals.Add(updatedDeal);
                                    tbStatus.Text = "Сделка добавлена";
                                }
                            }
                            else
                            {
                                // Если всё же null, обновляем весь список
                                _dataLoader.ClearCache();
                                MessageBox.Show("Сделка обновлена, обновите список", "Информация",
                                    MessageBoxButton.OK, MessageBoxImage.Information);
                            }
                        });

                        // Обновляем кеш
                        _dataLoader.ClearCache();
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Ошибка обновления: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    finally
                    {
                        HideProgressDialog();
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите сделку для редактирования", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void BtnDeleteDeal_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser?.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут удалять сделки", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dgDeals.SelectedItem is DealDto selectedDeal)
            {
                var result = MessageBox.Show($"Удалить сделку #{selectedDeal.Id}?",
                    "Подтверждение удаления",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        ShowProgressDialog("Удаление сделки", "Удаление...");

                        var success = await Task.Run(async () =>
                        {
                            return await _apiService.DeleteDealAsync(selectedDeal.Id);
                        });

                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (success)
                            {
                                _deals.Remove(selectedDeal);
                                tbStatus.Text = "Сделка успешно удалена";
                            }
                        });

                        // Обновляем кеш
                        _dataLoader.ClearCache();
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show($"Ошибка удаления: {ex.Message}", "Ошибка",
                                MessageBoxButton.OK, MessageBoxImage.Error);
                        });
                    }
                    finally
                    {
                        HideProgressDialog();
                    }
                }
            }
            else
            {
                MessageBox.Show("Выберите сделку для удаления", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void CbDealStatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_currentUser == null || cbDealStatusFilter.SelectedItem == null)
                return;

            if (cbDealStatusFilter.SelectedItem is ComboBoxItem selectedItem)
            {
                string status = selectedItem.Content.ToString();

                // Преобразуем текст в статус
                string statusFilter = status switch
                {
                    "Все" => "",
                    "В ожидании" => "В ожидании",
                    "Завершено" => "Завершено",
                    "Отменено" => "Отменено",
                    _ => ""
                };

                FilterDealsByStatus(statusFilter);
            }
        }


        private async Task StartLoadingAsync(string message)
        {
            if (_isLoading)
            {
                MessageBox.Show("Загрузка уже выполняется", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _isLoading = true;
            UpdateUIForLoading(true);

            try
            {
                ShowProgressDialog("Обновление", message);
                await _dataLoader.LoadAllDataAsync(); 
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideProgressDialog();
                _isLoading = false; 
                UpdateUIForLoading(false); 
            }
        }

        private void BtnCancelRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoading)
            {
                return;
            }

            _dataLoader.CancelLoading();
            _refreshTokenSource?.Cancel();
            tbStatus.Text = "Загрузка отменена";

            _isLoading = false;
            UpdateUIForLoading(false);
            HideProgressDialog();
        }

        private void CbAutoRefresh_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            // Определяем, какой чекбокс был изменен
            if (checkBox.Name == "cbAutoRefreshProperties" ||
                checkBox.Name == "cbAutoRefreshClients" ||
                checkBox.Name == "cbAutoRefreshDeals")
            {
                // Локальное автообновление для конкретного раздела
                // Можно реализовать отдельные таймеры для каждого раздела
                _isAutoRefreshEnabled = checkBox.IsChecked == true;

                if (_currentUser != null && _isAutoRefreshEnabled)
                {
                    _autoRefreshTimer.Start();
                }
                else
                {
                    _autoRefreshTimer.Stop();
                }
            }
        }

        private void CbAutoRefresh_Unchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox == null) return;

            // Проверяем, все ли чекбоксы отключены
            bool anyChecked = cbAutoRefreshProperties.IsChecked == true ||
                             cbAutoRefreshClients.IsChecked == true ||
                             cbAutoRefreshDeals.IsChecked == true;

            _isAutoRefreshEnabled = anyChecked;

            if (!_isAutoRefreshEnabled)
            {
                _autoRefreshTimer.Stop();
            }
        }

        private void UpdateUIForLoading(bool isLoading)
        {
            Dispatcher.Invoke(() =>
            {
                _isLoading = isLoading;

                // Показываем/скрываем кнопки отмены в зависимости от активного таба
                var currentTab = tcMain.SelectedItem as TabItem;
                if (currentTab != null)
                {
                    switch (currentTab.Header.ToString())
                    {
                        case "Объекты недвижимости":
                            btnCancelRefreshProperties.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
                            break;
                        case "Клиенты":
                            btnCancelRefreshClients.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
                            break;
                        case "Сделки":
                            btnCancelRefreshDeals.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
                            break;
                    }
                }

                // Блокируем кнопки обновления во время загрузки
                btnRefreshProperties.IsEnabled = !isLoading;
                btnRefreshClients.IsEnabled = !isLoading;
                btnRefreshDeals.IsEnabled = !isLoading;

                if (isLoading)
                {
                    // Во время загрузки блокируем все кнопки редактирования
                    btnAddProperty.IsEnabled = false;
                    btnEditProperty.IsEnabled = false;
                    btnDeleteProperty.IsEnabled = false;

                    btnAddClient.IsEnabled = false;
                    btnEditClient.IsEnabled = false;
                    btnDeleteClient.IsEnabled = false;

                    btnAddDeal.IsEnabled = false;
                    btnEditDeal.IsEnabled = false;
                    btnDeleteDeal.IsEnabled = false;
                }
                else
                {
                    UpdateEditButtons();
                }

                // Остальные элементы
                bool isAdmin = _currentUser?.Role == "Admin";
                btnGenerateSalesReport.IsEnabled = !isLoading && isAdmin;
                btnGeneratePropertyReport.IsEnabled = !isLoading && isAdmin;
                btnExportReport.IsEnabled = !isLoading && isAdmin;

                // Чекбоксы автообновления
                cbAutoRefreshProperties.IsEnabled = !isLoading && isAdmin;
                cbAutoRefreshClients.IsEnabled = !isLoading && isAdmin;
                cbAutoRefreshDeals.IsEnabled = !isLoading && isAdmin;

                // Поиск и фильтры
                txtSearchProperty.IsEnabled = !isLoading;
                txtSearchClient.IsEnabled = !isLoading;
                cbDealStatusFilter.IsEnabled = !isLoading;
            });
        }

        // Методы для управления автообновлением
        private void EnableAutoRefresh(bool enable)
        {
            _isAutoRefreshEnabled = enable;
            if (enable && _currentUser != null)
            {
                _autoRefreshTimer.Start();
            }
            else
            {
                _autoRefreshTimer.Stop();
            }
        }

        private async Task AutoRefreshDataAsync()
        {
            if (_currentUser != null && _isAutoRefreshEnabled)
            {
                try
                {
                    await _dataLoader.LoadAllDataAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка автообновления: {ex.Message}");
                }
            }
        }

        // Метод для отображения диалога прогресса
        private void ShowProgressDialog(string title, string message)
        {
            Dispatcher.Invoke(() =>
            {
                if (_progressDialog == null || !_progressDialog.IsVisible)
                {
                    _progressDialog = new ProgressDialog
                    {
                        Owner = this,
                        Title = title,
                        Message = message,
                        IsIndeterminate = true
                    };
                    _progressDialog.Closed += (s, e) => _progressDialog = null;
                    _progressDialog.Show();
                }
                else
                {
                    _progressDialog.Message = message;
                }
            });
        }

        // Метод для скрытия диалога прогресса
        private void HideProgressDialog()
        {
            Dispatcher.Invoke(() =>
            {
                _progressDialog?.Close();
                _progressDialog = null;
            });
        }

        // Метод для обновления прогресса
        private void UpdateProgress(double value, string details = null)
        {
            Dispatcher.Invoke(() =>
            {
                if (_progressDialog != null && _progressDialog.IsVisible)
                {
                    _progressDialog.ProgressValue = value;
                    if (!string.IsNullOrEmpty(details))
                    {
                        _progressDialog.Details = details;
                    }
                }
            });
        }

        // Основной метод загрузки данных
        private async Task LoadDataAsync(bool showProgress = true)
        {
            if (_currentUser == null)
            {
                MessageBox.Show("Пожалуйста, авторизуйтесь", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                if (showProgress)
                {
                    ShowProgressDialog("Загрузка данных", "Пожалуйста, подождите...");
                    UpdateProgress(10, "Подготовка...");
                }

                // Отменяем предыдущие операции загрузки
                _refreshTokenSource?.Cancel();
                _refreshTokenSource = new CancellationTokenSource();

                if (showProgress)
                {
                    UpdateProgress(30, "Загрузка объектов...");
                }

                // Используем фоновую загрузку
                await _dataLoader.LoadAllDataAsync();

                if (showProgress)
                {
                    UpdateProgress(100, "Загрузка завершена");
                    HideProgressDialog();
                }

                tbStatus.Text = $"Загружено: {_properties.Count} объектов, {_clients.Count} клиентов, {_deals.Count} сделок";
                tbConnectionStatus.Text = "Подключено";

                // Включаем автообновление если нужно
                if (_isAutoRefreshEnabled)
                {
                    _autoRefreshTimer.Start();
                }
            }
            catch (OperationCanceledException)
            {
                tbStatus.Text = "Загрузка отменена";
                HideProgressDialog();
            }
            catch (Exception ex)
            {
                HideProgressDialog();
                tbStatus.Text = $"Ошибка: {ex.Message}";
                tbConnectionStatus.Text = "Ошибка подключения";
                MessageBox.Show($"Ошибка загрузки данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод для обработки закрытия окна
        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _autoRefreshTimer.Stop();
            _refreshTokenSource?.Cancel();
            _dataLoader.Dispose();
        }

        // ===== МЕТОДЫ ДЛЯ ОТЧЕТОВ =====

        private void BtnGenerateSalesReport_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser?.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут генерировать отчеты", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ShowProgressDialog("Генерация отчета", "Формирование отчета по продажам...");

                // TODO: Реализовать логику генерации отчета по продажам
                // Можно вызвать метод API или использовать локальные данные

                MessageBox.Show("Отчет по продажам сгенерирован", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка генерации отчета: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideProgressDialog();
            }
        }

        private void BtnGeneratePropertyReport_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser?.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут генерировать отчеты", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ShowProgressDialog("Анализ объектов", "Анализ объектов недвижимости...");

                // TODO: Реализовать логику анализа объектов
                // Анализ доступных/проданных объектов, статистика по типам и ценам

                MessageBox.Show("Анализ объектов выполнен", "Успех",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка анализа объектов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideProgressDialog();
            }
        }

        private void UpdateEditButtons()
        {
            // Всегда выполняем в UI потоке
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(UpdateEditButtons);
                return;
            }

            bool isAdmin = _currentUser?.Role == "Admin";
            bool hasUser = _currentUser != null;

            // Для объектов недвижимости
            btnAddProperty.IsEnabled = isAdmin;
            btnEditProperty.IsEnabled = isAdmin && dgProperties.SelectedItem != null;
            btnDeleteProperty.IsEnabled = isAdmin && dgProperties.SelectedItem != null;

            // Для клиентов
            btnAddClient.IsEnabled = hasUser;
            btnEditClient.IsEnabled = hasUser && dgClients.SelectedItem != null;
            btnDeleteClient.IsEnabled = isAdmin && dgClients.SelectedItem != null;

            // Для сделок
            btnAddDeal.IsEnabled = hasUser;
            btnEditDeal.IsEnabled = hasUser && dgDeals.SelectedItem != null;
            btnDeleteDeal.IsEnabled = isAdmin && dgDeals.SelectedItem != null;
        }

        private void BtnReports_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser?.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут просматривать отчеты",
                    "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                var reportsWindow = new ReportsWindow(_apiService);
                reportsWindow.Owner = this;
                reportsWindow.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка открытия отчетов: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnExportReport_Click(object sender, RoutedEventArgs e)
        {
            if (_currentUser?.Role != "Admin")
            {
                MessageBox.Show("Только администраторы могут экспортировать отчеты", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Создаем диалог сохранения файла
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PDF files (*.pdf)|*.pdf|Excel files (*.xlsx)|*.xlsx|CSV files (*.csv)|*.csv",
                    DefaultExt = ".pdf",
                    FileName = $"Отчет_{DateTime.Now:yyyyMMdd_HHmmss}"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    ShowProgressDialog("Экспорт отчета", "Экспорт данных...");

                    // Определяем тип файла по расширению
                    var extension = System.IO.Path.GetExtension(saveFileDialog.FileName).ToLower();
                    var reportExporter = new ReportExporter();

                    // TODO: Получить данные для отчета (заглушка)
                    var salesReport = new SalesReportDto
                    {
                        StartDate = DateTime.Now.AddMonths(-1),
                        EndDate = DateTime.Now,
                        TotalRevenue = _deals.Where(d => d.Status == "Завершено").Sum(d => d.DealAmount),
                        TotalDeals = _deals.Count,
                        CompletedDeals = _deals.Count(d => d.Status == "Завершено"),
                        PendingDeals = _deals.Count(d => d.Status == "В ожидании"),
                        AverageDealAmount = _deals.Any() ? _deals.Average(d => d.DealAmount) : 0,
                        AgentStatistics = new List<AgentStatisticsDto>()
                        // Здесь нужно добавить реальную статистику по агентам
                    };

                    bool success = false;

                    if (extension == ".pdf")
                    {
                        success = await reportExporter.ExportToPdfAsync(salesReport, saveFileDialog.FileName);
                    }
                    else if (extension == ".xlsx" || extension == ".csv")
                    {
                        success = await reportExporter.ExportToExcelAsync(salesReport, saveFileDialog.FileName);
                    }
                    else
                    {
                        MessageBox.Show("Неподдерживаемый формат файла", "Ошибка",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    if (success)
                    {
                        MessageBox.Show($"Отчет успешно экспортирован в файл:\n{saveFileDialog.FileName}", "Успех",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка экспорта: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                HideProgressDialog();
            }
        }

        private void FilterDealsByStatus(string status)
        {
            Dispatcher.Invoke(() =>
            {
                if (string.IsNullOrEmpty(status))
                {
                    // Показываем все сделки
                    _deals.Clear();
                    foreach (var deal in _allDeals)
                    {
                        _deals.Add(deal);
                    }
                }
                else
                {
                    // Фильтруем по статусу
                    var filtered = _allDeals.Where(d =>
                        d.Status.Equals(status, StringComparison.OrdinalIgnoreCase) ||
                        (status == "В ожидании" && d.Status.Contains("ожидании")) ||
                        (status == "Завершено" && d.Status.Contains("Завершено")) ||
                        (status == "Отменено" && d.Status.Contains("Отменено")));

                    _deals.Clear();
                    foreach (var deal in filtered)
                    {
                        _deals.Add(deal);
                    }
                }

                tbStatus.Text = $"Показано сделок: {_deals.Count} из {_allDeals.Count}";
            });
        }
    }

    // Вспомогательные классы
    public enum DataType
    {
        Properties,
        Clients,
        Deals
    }

    public class DataLoadedEventArgs : EventArgs
    {
        public DataType DataType { get; set; }
        public object Data { get; set; }
    }

    public class BackgroundDataLoader : IDisposable
    {
        private readonly ApiService _apiService;
        private readonly Dispatcher _dispatcher;
        private CancellationTokenSource _cancellationTokenSource;
        private DateTime _lastCacheTime;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(1);

        public event EventHandler<DataLoadedEventArgs> DataLoaded;
        public event EventHandler<string> LoadingStatusChanged;
        public event EventHandler<Exception> LoadingError;

        public BackgroundDataLoader(ApiService apiService, Dispatcher dispatcher)
        {
            _apiService = apiService;
            _dispatcher = dispatcher;
            _cancellationTokenSource = new CancellationTokenSource();
            _lastCacheTime = DateTime.MinValue;
        }

        public async Task LoadAllDataAsync(bool forceRefresh = false)
        {
            try
            {
                OnLoadingStatusChanged("Начало загрузки данных...");

                // Загружаем все данные параллельно
                var propertiesTask = LoadPropertiesAsync(forceRefresh);
                var clientsTask = LoadClientsAsync(forceRefresh);
                var dealsTask = LoadDealsAsync(forceRefresh);

                await Task.WhenAll(propertiesTask, clientsTask, dealsTask);

                _lastCacheTime = DateTime.Now;
                OnLoadingStatusChanged("Данные успешно загружены");
            }
            catch (OperationCanceledException)
            {
                OnLoadingStatusChanged("Загрузка отменена");
            }
            catch (Exception ex)
            {
                OnLoadingError(ex);
            }
        }

        public async Task LoadPropertiesAsync(bool forceRefresh = false)
        {
            var properties = await _apiService.GetPropertiesAsync();
            OnDataLoaded(new DataLoadedEventArgs
            {
                DataType = DataType.Properties,
                Data = properties
            });
        }

        private async Task LoadClientsAsync(bool forceRefresh = false)
        {
            var clients = await _apiService.GetClientsAsync();
            OnDataLoaded(new DataLoadedEventArgs
            {
                DataType = DataType.Clients,
                Data = clients
            });
        }

        public async Task LoadDealsAsync(bool forceRefresh = false, string status = "")
        {
            try
            {
                OnLoadingStatusChanged("Загрузка сделок...");

                List<DealDto> deals;
                if (string.IsNullOrEmpty(status))
                {
                    // Загружаем все сделки
                    deals = await _apiService.GetDealsAsync();
                }
                else
                {
                    // Фильтруем на сервере по статусу
                    deals = await _apiService.GetDealsAsync();
                    deals = deals.Where(d =>
                        d.Status.Equals(status, StringComparison.OrdinalIgnoreCase) ||
                        d.Status.Contains(status)).ToList();
                }

                OnDataLoaded(new DataLoadedEventArgs
                {
                    DataType = DataType.Deals,
                    Data = deals
                });
            }
            catch (Exception ex)
            {
                OnLoadingError(ex);
            }
        }

        public bool IsCacheValid()
        {
            return (DateTime.Now - _lastCacheTime) < _cacheDuration;
        }

        public void ClearCache()
        {
            _lastCacheTime = DateTime.MinValue;
        }

        protected virtual void OnDataLoaded(DataLoadedEventArgs e)
        {
            DataLoaded?.Invoke(this, e);
        }

        protected virtual void OnLoadingStatusChanged(string status)
        {
            LoadingStatusChanged?.Invoke(this, status);
        }

        protected virtual void OnLoadingError(Exception ex)
        {
            LoadingError?.Invoke(this, ex);
        }

        public void CancelLoading()
        {
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
        }
    }
}