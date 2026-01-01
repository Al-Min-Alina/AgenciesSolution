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

            // Привязка данных
            dgProperties.ItemsSource = _properties;
            dgClients.ItemsSource = _clients;
            dgDeals.ItemsSource = _deals;

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
                        if (e.Data is List<DealDto> deals)
                        {
                            foreach (var deal in deals)
                            {
                                _deals.Add(deal);
                            }
                        }
                        break;
                }

                // Проверяем, завершена ли загрузка всех данных
                CheckLoadingComplete();
            });
        }

        private void CheckLoadingComplete()
        {
            // Этот метод можно расширить для более точного отслеживания загрузки
            // Пока просто сбрасываем флаг загрузки при любом обновлении данных
            if (_isLoading)
            {
                _isLoading = false;
                UpdateUIForLoading(false);
            }
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
                    btnAddProperty.IsEnabled = _currentUser.Role == "Admin";
                    btnEditProperty.IsEnabled = _currentUser.Role == "Admin";
                    btnDeleteProperty.IsEnabled = _currentUser.Role == "Admin";

                    // Включаем чекбоксы автообновления только для админов
                    cbAutoRefreshProperties.IsEnabled = _currentUser.Role == "Admin";
                    cbAutoRefreshClients.IsEnabled = _currentUser.Role == "Admin";
                    cbAutoRefreshDeals.IsEnabled = _currentUser.Role == "Admin";
                }
                else
                {
                    tbUsername.Text = "Не авторизован";
                    btnLogin.IsEnabled = true;
                    btnLogout.IsEnabled = false;

                    // Блокируем функционал
                    tabReports.IsEnabled = false;
                    btnAddProperty.IsEnabled = false;
                    btnEditProperty.IsEnabled = false;
                    btnDeleteProperty.IsEnabled = false;

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
                // 🔥 ПЕРЕДАЙТЕ ID И ОБЪЕКТ
                var dialog = new PropertyDialog(selectedProperty.Id, selectedProperty);

                if (dialog.ShowDialog() == true)
                {
                    try
                    {
                        ShowProgressDialog("Обновление объекта", "Сохранение изменений...");

                        var updatedProperty = await Task.Run(async () =>
                        {
                            return await _apiService.UpdatePropertyAsync(
                                selectedProperty.Id, dialog.UpdateRequest);
                        });

                        await Dispatcher.InvokeAsync(() =>
                        {
                            // Обновляем элемент в коллекции
                            var index = _properties.IndexOf(selectedProperty);
                            _properties[index] = updatedProperty;

                            tbStatus.Text = "Объект успешно обновлен";
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

            var dialog = new ClientDialog(_apiService)
            {
                IsAdmin = _currentUser.Role == "Admin",
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
                // Проверка прав для редактирования
                if (_currentUser.Role != "Admin" && selectedClient.AgentId != _currentUser.Id)
                {
                    MessageBox.Show("Вы можете редактировать только своих клиентов", "Доступ запрещен",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var dialog = new ClientDialog(_apiService, selectedClient)
                {
                    IsAdmin = _currentUser.Role == "Admin",
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
                            // Обновляем элемент в коллекции
                            var index = _clients.IndexOf(selectedClient);
                            _clients[index] = updatedClient;

                            tbStatus.Text = "Клиент успешно обновлен";
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
                // Проверка прав для редактирования
                if (_currentUser.Role != "Admin" && selectedDeal.AgentId != _currentUser.Id)
                {
                    MessageBox.Show("Вы можете редактировать только свои сделки", "Доступ запрещен",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
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
                            // Обновляем элемент в коллекции
                            var index = _deals.IndexOf(selectedDeal);
                            _deals[index] = updatedDeal;

                            tbStatus.Text = "Сделка успешно обновлена";
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
            if (cbDealStatusFilter.SelectedItem is ComboBoxItem selectedItem && _currentUser != null)
            {
                var status = selectedItem.Tag as string;
                // Здесь нужно реализовать фильтрацию сделок по статусу
            }
        }

        // ===== НОВЫЕ МЕТОДЫ ДЛЯ УПРАВЛЕНИЯ ЗАГРУЗКОЙ =====

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
                await LoadDataAsync(false);
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

                // Блокируем кнопки во время загрузки
                bool isAdmin = _currentUser?.Role == "Admin";
                bool canEdit = !isLoading && isAdmin;

                btnRefreshProperties.IsEnabled = !isLoading;
                btnRefreshClients.IsEnabled = !isLoading;
                btnRefreshDeals.IsEnabled = !isLoading;

                btnAddProperty.IsEnabled = canEdit;
                btnEditProperty.IsEnabled = canEdit && dgProperties.SelectedItem != null;
                btnDeleteProperty.IsEnabled = canEdit && dgProperties.SelectedItem != null;

                btnAddClient.IsEnabled = !isLoading && _currentUser != null;
                btnEditClient.IsEnabled = !isLoading && _currentUser != null && dgClients.SelectedItem != null;
                btnDeleteClient.IsEnabled = !isLoading && isAdmin && dgClients.SelectedItem != null;

                btnAddDeal.IsEnabled = !isLoading && _currentUser != null;
                btnEditDeal.IsEnabled = !isLoading && _currentUser != null && dgDeals.SelectedItem != null;
                btnDeleteDeal.IsEnabled = !isLoading && isAdmin && dgDeals.SelectedItem != null;

                btnGenerateSalesReport.IsEnabled = !isLoading && isAdmin;
                btnGeneratePropertyReport.IsEnabled = !isLoading && isAdmin;
                btnExportReport.IsEnabled = !isLoading && isAdmin;

                // Блокируем чекбоксы автообновления во время загрузки
                cbAutoRefreshProperties.IsEnabled = !isLoading && isAdmin;
                cbAutoRefreshClients.IsEnabled = !isLoading && isAdmin;
                cbAutoRefreshDeals.IsEnabled = !isLoading && isAdmin;

                // Блокируем поиск и фильтры
                txtSearchProperty.IsEnabled = !isLoading;
                txtSearchClient.IsEnabled = !isLoading;
                cbDealStatusFilter.IsEnabled = !isLoading;
            });
        }

        // ===== СУЩЕСТВУЮЩИЕ МЕТОДЫ =====

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
                    // Логируем ошибку, но не показываем пользователю при автообновлении
                    Console.WriteLine($"Ошибка автообновления: {ex.Message}");
                }
            }
        }

        // Метод для отображения диалога прогресса (улучшенная версия)
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
                        TotalRevenue = _deals.Where(d => d.Status == "Completed").Sum(d => d.DealAmount),
                        TotalDeals = _deals.Count,
                        CompletedDeals = _deals.Count(d => d.Status == "Completed"),
                        PendingDeals = _deals.Count(d => d.Status == "Pending"),
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

        private async Task LoadPropertiesAsync(bool forceRefresh = false)
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

        private async Task LoadDealsAsync(bool forceRefresh = false, string status = "")
        {
            var deals = await _apiService.GetDealsAsync(status);
            OnDataLoaded(new DataLoadedEventArgs
            {
                DataType = DataType.Deals,
                Data = deals
            });
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