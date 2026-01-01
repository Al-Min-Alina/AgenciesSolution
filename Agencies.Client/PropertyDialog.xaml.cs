using Agencies.Core.DTO;
using Newtonsoft.Json;
using System;
using System.Net.Http.Headers;
using System.Net.Http;
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
            if (!ValidateInput())
                return;

            try
            {
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
                btnSave.IsEnabled = true;
                btnSave.Content = "Сохранить";
            }
        }

        private async Task<bool> SaveToServer()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    // Настройте базовый адрес API
                    client.BaseAddress = new Uri("http://localhost:7149/");

                    // Добавьте токен авторизации (если нужно)
                    // client.DefaultRequestHeaders.Authorization = 
                    //     new AuthenticationHeaderValue("Bearer", "ваш_токен");

                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    if (IsUpdate)
                    {
                        // Для обновления
                        var updateRequest = new UpdatePropertyRequest
                        {
                            Title = txtTitle.Text,
                            Description = txtDescription.Text,
                            Address = txtAddress.Text,
                            Price = double.Parse(txtPrice.Text),
                            Area = double.Parse(txtArea.Text),
                            Type = (cbType.SelectedItem as ComboBoxItem)?.Content.ToString(),
                            Rooms = int.Parse(txtRooms.Text),
                            IsAvailable = cbIsAvailable.IsChecked ?? true
                        };

                        string json = JsonConvert.SerializeObject(updateRequest);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await client.PutAsync($"api/properties/{_propertyId}", content);

                        return response.IsSuccessStatusCode;
                    }
                    else
                    {
                        // Для создания
                        var createRequest = new CreatePropertyRequest
                        {
                            Title = txtTitle.Text,
                            Description = txtDescription.Text,
                            Address = txtAddress.Text,
                            Price = double.Parse(txtPrice.Text),
                            Area = double.Parse(txtArea.Text),
                            Type = (cbType.SelectedItem as ComboBoxItem)?.Content.ToString(),
                            Rooms = int.Parse(txtRooms.Text),
                            IsAvailable = cbIsAvailable.IsChecked ?? true
                        };

                        string json = JsonConvert.SerializeObject(createRequest);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync("api/properties", content);

                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при сохранении на сервере: {ex.Message}",
                    "Ошибка сети", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool ValidateInput()
        {
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

            if (!decimal.TryParse(txtPrice.Text, out decimal price) || price <= 0)
            {
                MessageBox.Show("Введите корректную цену", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPrice.Focus();
                return false;
            }

            if (!double.TryParse(txtArea.Text, out double area) || area <= 0)
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