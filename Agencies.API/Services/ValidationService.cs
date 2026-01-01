using Agencies.Core.DTO;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace Agencies.Client.Services
{
    public class ValidationService
    {
        public ValidationResult ValidateProperty(CreatePropertyRequest property)
        {
            var errors = new Dictionary<string, List<string>>();

            if (string.IsNullOrWhiteSpace(property.Title))
            {
                AddError(errors, nameof(property.Title), "Название обязательно");
            }
            else if (property.Title.Length < 5)
            {
                AddError(errors, nameof(property.Title), "Название должно содержать минимум 5 символов");
            }
            else if (property.Title.Length > 200)
            {
                AddError(errors, nameof(property.Title), "Название не должно превышать 200 символов");
            }

            if (string.IsNullOrWhiteSpace(property.Address))
            {
                AddError(errors, nameof(property.Address), "Адрес обязателен");
            }
            else if (property.Address.Length > 500)
            {
                AddError(errors, nameof(property.Address), "Адрес не должен превышать 500 символов");
            }

            if (property.Price <= 0)
            {
                AddError(errors, nameof(property.Price), "Цена должна быть больше 0");
            }
            else if (property.Price > 1000000000)
            {
                AddError(errors, nameof(property.Price), "Цена не должна превышать 1 000 000 000");
            }

            if (property.Area <= 0)
            {
                AddError(errors, nameof(property.Area), "Площадь должна быть больше 0");
            }
            else if (property.Area > 10000)
            {
                AddError(errors, nameof(property.Area), "Площадь не должна превышать 10 000 м²");
            }

            if (string.IsNullOrWhiteSpace(property.Type))
            {
                AddError(errors, nameof(property.Type), "Тип недвижимости обязателен");
            }
            else if (!IsValidPropertyType(property.Type))
            {
                AddError(errors, nameof(property.Type), "Недопустимый тип недвижимости");
            }

            if (property.Rooms < 0 || property.Rooms > 100)
            {
                AddError(errors, nameof(property.Rooms), "Количество комнат должно быть от 0 до 100");
            }

            if (property.Description?.Length > 2000)
            {
                AddError(errors, nameof(property.Description), "Описание не должно превышать 2000 символов");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        public ValidationResult ValidateClient(CreateClientRequest client)
        {
            var errors = new Dictionary<string, List<string>>();

            if (string.IsNullOrWhiteSpace(client.FirstName))
            {
                AddError(errors, nameof(client.FirstName), "Имя обязательно");
            }
            else if (client.FirstName.Length > 100)
            {
                AddError(errors, nameof(client.FirstName), "Имя не должно превышать 100 символов");
            }
            else if (!IsValidName(client.FirstName))
            {
                AddError(errors, nameof(client.FirstName), "Имя может содержать только буквы, пробелы и дефисы");
            }

            if (string.IsNullOrWhiteSpace(client.LastName))
            {
                AddError(errors, nameof(client.LastName), "Фамилия обязательна");
            }
            else if (client.LastName.Length > 100)
            {
                AddError(errors, nameof(client.LastName), "Фамилия не должна превышать 100 символов");
            }
            else if (!IsValidName(client.LastName))
            {
                AddError(errors, nameof(client.LastName), "Фамилия может содержать только буквы, пробелы и дефисы");
            }

            if (string.IsNullOrWhiteSpace(client.Email))
            {
                AddError(errors, nameof(client.Email), "Email обязателен");
            }
            else if (!IsValidEmail(client.Email))
            {
                AddError(errors, nameof(client.Email), "Неверный формат email");
            }
            else if (client.Email.Length > 200)
            {
                AddError(errors, nameof(client.Email), "Email не должен превышать 200 символов");
            }

            if (string.IsNullOrWhiteSpace(client.Phone))
            {
                AddError(errors, nameof(client.Phone), "Телефон обязателен");
            }
            else if (client.Phone.Length > 20)
            {
                AddError(errors, nameof(client.Phone), "Телефон не должен превышать 20 символов");
            }
            else if (!IsValidPhone(client.Phone))
            {
                AddError(errors, nameof(client.Phone), "Неверный формат телефона");
            }

            if (client.Budget < 0)
            {
                AddError(errors, nameof(client.Budget), "Бюджет не может быть отрицательным");
            }
            else if (client.Budget > 1000000000)
            {
                AddError(errors, nameof(client.Budget), "Бюджет не должен превышать 1 000 000 000");
            }

            if (client.Requirements?.Length > 1000)
            {
                AddError(errors, nameof(client.Requirements), "Требования не должны превышать 1000 символов");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        public ValidationResult ValidateDeal(CreateDealRequest deal)
        {
            var errors = new Dictionary<string, List<string>>();

            if (deal.PropertyId <= 0)
            {
                AddError(errors, nameof(deal.PropertyId), "ID объекта недвижимости обязательно");
            }

            if (deal.ClientId <= 0)
            {
                AddError(errors, nameof(deal.ClientId), "ID клиента обязательно");
            }

            if (deal.DealAmount <= 0)
            {
                AddError(errors, nameof(deal.DealAmount), "Сумма сделки должна быть больше 0");
            }
            else if (deal.DealAmount > 1000000000)
            {
                AddError(errors, nameof(deal.DealAmount), "Сумма сделки не должна превышать 1 000 000 000");
            }

            if (deal.DealDate == DateTime.MinValue)
            {
                AddError(errors, nameof(deal.DealDate), "Дата сделки обязательна");
            }
            else if (deal.DealDate > DateTime.Now)
            {
                AddError(errors, nameof(deal.DealDate), "Дата сделки не может быть в будущем");
            }
            else if (deal.DealDate < DateTime.Now.AddYears(-10))
            {
                AddError(errors, nameof(deal.DealDate), "Дата сделки не может быть старше 10 лет");
            }

            if (string.IsNullOrWhiteSpace(deal.Status))
            {
                AddError(errors, nameof(deal.Status), "Статус сделки обязателен");
            }
            else if (!IsValidDealStatus(deal.Status))
            {
                AddError(errors, nameof(deal.Status), "Недопустимый статус сделки");
            }

            if (deal.AgentId <= 0)
            {
                AddError(errors, nameof(deal.AgentId), "ID агента обязательно");
            }

            return new ValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors
            };
        }

        private void AddError(Dictionary<string, List<string>> errors, string field, string message)
        {
            if (!errors.ContainsKey(field))
            {
                errors[field] = new List<string>();
            }
            errors[field].Add(message);
        }

        private bool IsValidPropertyType(string type)
        {
            var validTypes = new[] { "Apartment", "House", "Commercial", "Land", "Other" };
            return validTypes.Contains(type);
        }

        private bool IsValidName(string name)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(name, @"^[a-zA-Zа-яА-ЯёЁ\s-]+$");
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
            return System.Text.RegularExpressions.Regex.IsMatch(phone, @"^[\d\s\(\)\-\+]+$");
        }

        private bool IsValidDealStatus(string status)
        {
            var validStatuses = new[] { "Pending", "Completed", "Cancelled", "OnHold", "Failed" };
            return validStatuses.Contains(status);
        }
    }

    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public Dictionary<string, List<string>> Errors { get; set; }
        public string Summary => string.Join("\n",
            Errors.SelectMany(kvp => kvp.Value.Select(v => $"{kvp.Key}: {v}")));
    }
}