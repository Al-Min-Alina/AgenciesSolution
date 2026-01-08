using Agencies.Core.DTO;
using Newtonsoft.Json;
using System;
using System.Net.Http.Headers;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Agencies.Client
{
    public partial class PropertyDialog : Window
    {
        private int _propertyId;
        public CreatePropertyRequest Property { get; private set; }

        public UpdatePropertyRequest UpdateRequest { get; private set; }
        public bool IsUpdate { get; private set; }

        private bool _isSaving = false;
        private CancellationTokenSource _cts;

        public PropertyDialog()
        {
            InitializeComponent();
            Title = "Добавить объект";
            Property = new CreatePropertyRequest();
            IsUpdate = false;
        }

        public PropertyDialog(int propertyId, PropertyDto existingProperty)
        {
            InitializeComponent();
            Title = "Редактировать объект";

            // Создаем запрос из существующего объекта
            Property = new CreatePropertyRequest
            {
                Title = existingProperty.Title,
                Address = existingProperty.Address,
                Price = existingProperty.Price,
                Area = existingProperty.Area,
                Type = existingProperty.Type,
                Rooms = existingProperty.Rooms,
                Description = existingProperty.Description,
                IsAvailable = existingProperty.IsAvailable
            };
            _propertyId = propertyId;
            IsUpdate = true;

            // Заполняем поля
            txtTitle.Text = existingProperty.Title;
            txtAddress.Text = existingProperty.Address;
            txtPrice.Text = existingProperty.Price.ToString();
            txtArea.Text = existingProperty.Area.ToString();
            txtRooms.Text = existingProperty.Rooms.ToString();
            txtDescription.Text = existingProperty.Description;
            cbIsAvailable.IsChecked = existingProperty.IsAvailable;

            // Устанавливаем тип в ComboBox
            foreach (ComboBoxItem item in cbType.Items)
            {
                if (item.Content.ToString() == existingProperty.Type)
                {
                    cbType.SelectedItem = item;
                    break;
                }
            }
        }

        private async void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // ЗАЩИТА ОТ ДВОЙНОГО ВЫЗОВА
            if (_isSaving)
            {
                Console.WriteLine("=== ПРЕРВАНО: уже сохраняется ===");
                return;
            }

            Console.WriteLine("=== BtnSave_Click НАЧАЛО ===");

            if (!ValidateInput())
                return;

            try
            {
                _isSaving = true;
                _cts = new CancellationTokenSource();

                btnSave.IsEnabled = false;
                btnSave.Content = "Сохранение...";

                // Сохраняем на сервер
                bool success = await SaveToServer();

                if (success)
                {
                    MessageBox.Show("Данные успешно сохранены на сервере!",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    if (IsUpdate)
                    {
                        Property.Title = txtTitle.Text;
                        Property.Address = txtAddress.Text;
                        Property.Price = double.Parse(txtPrice.Text);
                        Property.Area = double.Parse(txtArea.Text);
                        Property.Type = (cbType.SelectedItem as ComboBoxItem)?.Content.ToString();
                        Property.Rooms = int.Parse(txtRooms.Text);
                        Property.Description = txtDescription.Text;
                        Property.IsAvailable = cbIsAvailable.IsChecked ?? true;
                    }
                    else
                    {
                        // Заполняем CreateRequest
                        Property = new CreatePropertyRequest
                        {
                            Title = txtTitle.Text,
                            Address = txtAddress.Text,
                            Price = double.Parse(txtPrice.Text),
                            Area = double.Parse(txtArea.Text),
                            Type = (cbType.SelectedItem as ComboBoxItem)?.Content.ToString(),
                            Rooms = int.Parse(txtRooms.Text),
                            Description = txtDescription.Text,
                            IsAvailable = cbIsAvailable.IsChecked ?? true
                        };
                    }
                    DialogResult = true;
                    Close();
                }
                else
                {
                    MessageBox.Show("Не удалось сохранить данные на сервере. Проверьте подключение.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка в данных: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _isSaving = false;
                _cts?.Dispose();
                _cts = null;

                btnSave.IsEnabled = true;
                btnSave.Content = "Сохранить";

                Console.WriteLine("=== BtnSave_Click КОНЕЦ ===");
            }
        }

        private async Task<bool> SaveToServer()
        {
            Console.WriteLine($"=== SaveToServer() ВЫЗВАН (IsUpdate={IsUpdate}) ===");

            //// Подробная проверка авторизации
            //Console.WriteLine($"[SaveToServer] Проверка App.IsAuthenticated()...");
            //var isAuth = App.IsAuthenticated();
            //Console.WriteLine($"[SaveToServer] App.IsAuthenticated() вернул: {isAuth}");

            //if (!isAuth)
            //{
            //    MessageBox.Show("Вы не авторизованы. Пожалуйста, войдите в систему.",
            //        "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Warning);
            //    return false;
            //}

            //// Проверяем токен в ApiService (используем App.ApiService)
            //Console.WriteLine($"[SaveToServer] Проверка токена в ApiService...");
            //var tokenStatus = App.ApiService.GetTokenStatus();
            //Console.WriteLine($"[SaveToServer] Статус токена: {tokenStatus}");

            //if (!App.ApiService.HasToken())
            //{
            //    MessageBox.Show("Токен авторизации не установлен. Пожалуйста, войдите снова.",
            //        "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Warning);

            //    // Пробуем восстановить токен из настроек
            //    Console.WriteLine($"[SaveToServer] Попытка восстановить токен из настроек...");
            //    var savedToken = Settings.Default.AuthToken;
            //    if (!string.IsNullOrEmpty(savedToken))
            //    {
            //        Console.WriteLine($"[SaveToServer] Восстанавливаем токен...");
            //        App.ApiService.SetToken(savedToken);
            //    }
            //    else
            //    {
            //        Console.WriteLine($"[SaveToServer] Токен не найден в настройках");
            //        return false;
            //    }
            //}

            try
            {
                var culture = System.Globalization.CultureInfo.InvariantCulture;

                if (IsUpdate)
                {
                    var updateRequest = new UpdatePropertyRequest
                    {
                        Title = txtTitle.Text.Trim(),
                        Description = txtDescription.Text.Trim(),
                        Address = txtAddress.Text.Trim(),
                        Price = double.Parse(txtPrice.Text.Replace(",", "."), culture),
                        Area = double.Parse(txtArea.Text.Replace(",", "."), culture),
                        Type = (cbType.SelectedItem as ComboBoxItem)?.Content.ToString(),
                        Rooms = int.Parse(txtRooms.Text),
                        IsAvailable = cbIsAvailable.IsChecked ?? true
                    };

                    Console.WriteLine($"Обновление объекта ID: {_propertyId}");

                    // Проверка подключения перед отправкой
                    //var isConnected = await App.ApiService.CheckConnectionAsync();
                    //if (!isConnected)
                    //{
                    //    MessageBox.Show("Нет подключения к серверу. Проверьте сетевое подключение.",
                    //        "Ошибка сети", MessageBoxButton.OK, MessageBoxImage.Error);
                    //    return false;
                    //}

                    var result = await App.ApiService.UpdatePropertyAsync(_propertyId, updateRequest);

                    if (result != null)
                    {
                        Console.WriteLine($"Объект успешно обновлен, ID: {result.Id}");
                        return true;
                    }
                }
                else
                {
                    var createRequest = new CreatePropertyRequest
                    {
                        Title = txtTitle.Text.Trim(),
                        Description = txtDescription.Text.Trim(),
                        Address = txtAddress.Text.Trim(),
                        Price = double.Parse(txtPrice.Text.Replace(",", "."), culture),
                        Area = double.Parse(txtArea.Text.Replace(",", "."), culture),
                        Type = (cbType.SelectedItem as ComboBoxItem)?.Content.ToString(),
                        Rooms = int.Parse(txtRooms.Text),
                        IsAvailable = cbIsAvailable.IsChecked ?? true
                    };

                    Console.WriteLine($"Создание нового объекта");

                    // Проверка подключения перед отправкой
                    //var isConnected = await App.ApiService.CheckConnectionAsync();
                    //if (!isConnected)
                    //{
                    //    MessageBox.Show("Нет подключения к серверу. Проверьте сетевое подключение.",
                    //        "Ошибка сети", MessageBoxButton.OK, MessageBoxImage.Error);
                    //    return false;
                    //}

                    var result = await App.ApiService.CreatePropertyAsync(createRequest);

                    if (result != null)
                    {
                        Console.WriteLine($"Объект успешно создан, ID: {result.Id}");
                        return true;
                    }
                }

                return false;
            }
            catch (System.Net.Http.HttpRequestException httpEx)
                when (httpEx.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                // Токен истек
                App.ClearSession();

                MessageBox.Show("Сессия истекла. Пожалуйста, войдите снова.",
                    "Сессия истекла", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
            catch (System.Net.Http.HttpRequestException httpEx)
                when (httpEx.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                MessageBox.Show("У вас недостаточно прав для выполнения этой операции.",
                    "Доступ запрещен", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (System.Net.Http.HttpRequestException httpEx)
            {
                // Общая ошибка сети
                Console.WriteLine($"Ошибка сети: {httpEx}");
                MessageBox.Show($"Ошибка подключения к серверу: {httpEx.Message}",
                    "Ошибка сети", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при сохранении: {ex}");
                MessageBox.Show($"Ошибка при сохранении: {ex.Message}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool ValidateInput()
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            if (string.IsNullOrWhiteSpace(txtTitle.Text))
            {
                MessageBox.Show("Введите название", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtTitle.Focus();
                return false;
            }

            if (string.IsNullOrWhiteSpace(txtAddress.Text))
            {
                MessageBox.Show("Введите адрес", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtAddress.Focus();
                return false;
            }

            if (!double.TryParse(txtPrice.Text.Replace(",", "."),
                System.Globalization.NumberStyles.Any, culture, out double price) || price <= 0)
            {
                MessageBox.Show("Введите корректную цену", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPrice.Focus();
                return false;
            }

            if (!double.TryParse(txtArea.Text.Replace(",", "."),
                System.Globalization.NumberStyles.Any, culture, out double area) || area <= 0)
            {
                MessageBox.Show("Введите корректную площадь", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtArea.Focus();
                return false;
            }

            if (cbType.SelectedItem == null)
            {
                MessageBox.Show("Выберите тип недвижимости", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                cbType.Focus();
                return false;
            }

            if (!int.TryParse(txtRooms.Text, out int rooms) || rooms < 0)
            {
                MessageBox.Show("Введите корректное количество комнат", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtRooms.Focus();
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
}