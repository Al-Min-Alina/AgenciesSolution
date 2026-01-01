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
            Console.WriteLine($"=== SaveToServer() ВЫЗВАН (IsUpdate={IsUpdate}, _propertyId={_propertyId}) ===");
            try
            {
                // ИСПОЛЬЗУЙТЕ InvariantCulture для парсинга чисел с запятой
                var culture = System.Globalization.CultureInfo.InvariantCulture;

                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri("https://localhost:7149/");

                    client.DefaultRequestHeaders.Authorization =
                        new AuthenticationHeaderValue("Bearer", "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1laWQiOiI3IiwidW5pcXVlX25hbWUiOiJBbGluYSIsImVtYWlsIjoiQWxpbmFAYmsucnUiLCJyb2xlIjoiQWRtaW4iLCJSb2xlIjoiQWRtaW4iLCJuYmYiOjE3NjcyNzc5NTcsImV4cCI6MTc2NzI4NTE1NywiaWF0IjoxNzY3Mjc3OTU3LCJpc3MiOiJBZ2VuY2llc0FQSV9EZXYiLCJhdWQiOiJBZ2VuY2llc0NsaWVudF9EZXYifQ.lliBzsncLqYSVOWWyCpd6MJVvfFgOrLtfW1QLbE4LWE");

                    client.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json"));

                    // УВЕЛИЧЬТЕ ТАЙМАУТ
                    client.Timeout = TimeSpan.FromSeconds(30);

                    if (IsUpdate)
                    {
                        // ИСПРАВЬТЕ ПАРСИНГ ЧИСЕЛ
                        var updateRequest = new UpdatePropertyRequest
                        {
                            Title = txtTitle.Text,
                            Description = txtDescription.Text,
                            Address = txtAddress.Text,
                            // ИСПОЛЬЗУЙТЕ InvariantCulture ДЛЯ ПАРСИНГА
                            Price = double.Parse(txtPrice.Text.Replace(",", "."), culture),
                            // ЗАМЕНИТЕ ЗАПЯТУЮ НА ТОЧКУ
                            Area = double.Parse(txtArea.Text.Replace(",", "."), culture),
                            Type = (cbType.SelectedItem as ComboBoxItem)?.Content.ToString(),
                            Rooms = int.Parse(txtRooms.Text),
                            IsAvailable = cbIsAvailable.IsChecked ?? true
                        };

                        // ЛОГИРУЙТЕ ДЛЯ ДИАГНОСТИКИ
                        Console.WriteLine($"=== CLIENT: Update Request ===");
                        Console.WriteLine($"Title: {updateRequest.Title}");
                        Console.WriteLine($"Price: {updateRequest.Price}");
                        Console.WriteLine($"Area: {updateRequest.Area}");
                        Console.WriteLine($"Rooms: {updateRequest.Rooms}");
                        Console.WriteLine($"Type: {updateRequest.Type}");

                        string json = JsonConvert.SerializeObject(updateRequest);
                        Console.WriteLine($"JSON: {json}");

                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        // ПРОВЕРЬТЕ, ЧТО ОТПРАВЛЯЕТСЯ
                        var contentString = await content.ReadAsStringAsync();
                        Console.WriteLine($"Content to send: {contentString}");
                        Console.WriteLine($"Content length: {contentString.Length}");

                        var response = await client.PutAsync($"api/properties/{_propertyId}", content);

                        // ПРОЧИТАЙТЕ ОТВЕТ ДАЖЕ ПРИ ОШИБКЕ
                        var responseContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Response Status: {(int)response.StatusCode} ({response.StatusCode})");
                        Console.WriteLine($"Response Content: {responseContent}");

                        if (!response.IsSuccessStatusCode)
                        {
                            // ПОКАЖИТЕ ПОЛЬЗОВАТЕЛЮ, ЧТО НА САМОМ ДЕЛЕ ВЕРНУЛ СЕРВЕР
                            try
                            {
                                var errorObj = JsonConvert.DeserializeObject<dynamic>(responseContent);
                                if (errorObj != null && errorObj.Message != null)
                                {
                                    MessageBox.Show($"Ошибка сервера: {errorObj.Message}",
                                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                                else if (errorObj != null && errorObj.errors != null)
                                {
                                    // Если есть ошибки валидации
                                    var errors = new StringBuilder();
                                    foreach (var error in errorObj.errors)
                                    {
                                        errors.AppendLine($"{error.Name}: {string.Join(", ", error.Errors.ToObject<string[]>())}");
                                    }
                                    MessageBox.Show($"Ошибки валидации:\n{errors}",
                                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                                }
                            }
                            catch
                            {
                                MessageBox.Show($"Ошибка обновления: {response.StatusCode}\n{responseContent}",
                                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }

                        return response.IsSuccessStatusCode;
                    }
                    else
                    {
                        // ТО ЖЕ САМОЕ ДЛЯ СОЗДАНИЯ
                        var createRequest = new CreatePropertyRequest
                        {
                            Title = txtTitle.Text,
                            Description = txtDescription.Text,
                            Address = txtAddress.Text,
                            Price = double.Parse(txtPrice.Text.Replace(",", "."), culture),
                            Area = double.Parse(txtArea.Text.Replace(",", "."), culture),
                            Type = (cbType.SelectedItem as ComboBoxItem)?.Content.ToString(),
                            Rooms = int.Parse(txtRooms.Text),
                            IsAvailable = cbIsAvailable.IsChecked ?? true
                        };

                        Console.WriteLine($"=== CLIENT: Create Request ===");
                        Console.WriteLine($"Title: {createRequest.Title}");
                        Console.WriteLine($"Area: {createRequest.Area}");

                        string json = JsonConvert.SerializeObject(createRequest);
                        var content = new StringContent(json, Encoding.UTF8, "application/json");

                        var response = await client.PostAsync("api/properties", content);

                        // ТАКЖЕ ПРОЧИТАЙТЕ ОТВЕТ
                        var responseContent = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"Response Status: {(int)response.StatusCode} ({response.StatusCode})");
                        Console.WriteLine($"Response Content: {responseContent}");

                        if (!response.IsSuccessStatusCode)
                        {
                            MessageBox.Show($"Ошибка создания: {response.StatusCode}\n{responseContent}",
                                "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        }

                        return response.IsSuccessStatusCode;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"=== SaveToServer ИСКЛЮЧЕНИЕ: {ex} ===");
                // ПОДРОБНОЕ СООБЩЕНИЕ ОБ ОШИБКЕ
                MessageBox.Show($"Ошибка при сохранении на сервере:\n\n" +
                               $"Тип: {ex.GetType().Name}\n" +
                               $"Сообщение: {ex.Message}\n" +
                               $"Внутренняя ошибка: {ex.InnerException?.Message}",
                    "Ошибка сети", MessageBoxButton.OK, MessageBoxImage.Error);

                // ЛОГИРУЙТЕ ДЛЯ ДИАГНОСТИКИ
                Console.WriteLine($"=== CLIENT EXCEPTION ===");
                Console.WriteLine(ex.ToString());

                return false;
            }
        }

        private bool ValidateInput()
        {
            // ИСПОЛЬЗУЙТЕ ТОТ ЖЕ ПОДХОД К ПАРСИНГУ
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

            // ИСПРАВЬТЕ ПАРСИНГ ЦЕНЫ
            if (!decimal.TryParse(txtPrice.Text.Replace(",", "."),
                System.Globalization.NumberStyles.Any, culture, out decimal price) || price <= 0)
            {
                MessageBox.Show("Введите корректную цену", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtPrice.Focus();
                return false;
            }

            // ИСПРАВЬТЕ ПАРСИНГ ПЛОЩАДИ
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