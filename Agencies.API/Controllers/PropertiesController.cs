using Agencies.Core.DTO;
using Agencies.Domain.Models;
using Agencies.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Agencies.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Policy = "AdminOrUser")]
    public class PropertiesController : ControllerBase
    {
        private readonly IPropertyRepository _propertyRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<PropertiesController> _logger;

        public PropertiesController(
            IPropertyRepository propertyRepository,
            IUserRepository userRepository,
            ILogger<PropertiesController> logger)
        {
            _propertyRepository = propertyRepository;
            _userRepository = userRepository;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<PropertyDto>>> GetProperties()
        {
            try
            {
                var properties = await _propertyRepository.GetAllAsync();

                var propertyDtos = properties.Select(p => MapToDto(p)).ToList();

                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole != "Admin")
                {
                    propertyDtos = propertyDtos.Where(p => p.IsAvailable).ToList();
                }

                return Ok(propertyDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting properties");
                throw;
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<PropertyDto>> GetProperty(int id)
        {
            try
            {
                var property = await _propertyRepository.GetByIdAsync(id);

                if (property == null)
                {
                    throw new NotFoundException(nameof(Property), id);
                }

                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (userRole != "Admin" && (!property.IsAvailable || property.CreatedByUserId.ToString() != userId))
                {
                    throw new UnauthorizedAccessException("Доступ к объекту запрещен");
                }

                return Ok(MapToDto(property));
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (UnauthorizedAccessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting property with id {PropertyId}", id);
                throw;
            }
        }

        [HttpPost]
        [Authorize(Policy = "AdminOnly")]
        public async Task<ActionResult<PropertyDto>> CreateProperty([FromBody] CreatePropertyRequest request)
        {

            // ДИАГНОСТИКА: логируем полученный запрос
            Console.WriteLine($"=== DIAGNOSTIC: Received request ===");
            Console.WriteLine($"Title: '{request.Title}' (Length: {request.Title?.Length})");
            Console.WriteLine($"Type: '{request.Type}'");
            Console.WriteLine($"Price: {request.Price}");
            Console.WriteLine($"Area: {request.Area}");
            Console.WriteLine($"Rooms: {request.Rooms}");
            Console.WriteLine($"================================");

            if (!ModelState.IsValid)
            {
                Console.WriteLine("=== VALIDATION ERRORS ===");
                foreach (var key in ModelState.Keys)
                {
                    var errors = ModelState[key].Errors;
                    if (errors.Any())
                    {
                        Console.WriteLine($"{key}:");
                        foreach (var error in errors)
                        {
                            Console.WriteLine($"  - {error.ErrorMessage}");
                        }
                    }
                }
                return BadRequest(ModelState);
            }

            try
            {
                var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "0");

                // Валидация уже выполнена через ValidationFilter и валидатор
                // Удалите повторные проверки из контроллера!

                var property = new Property
                {
                    Title = request.Title,
                    Description = request.Description ?? string.Empty,
                    Address = request.Address,
                    Price = request.Price,
                    Area = request.Area,
                    Type = request.Type,
                    Rooms = request.Rooms,
                    IsAvailable = request.IsAvailable,
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = userId > 0 ? userId : null
                };

                var createdProperty = await _propertyRepository.AddAsync(property);

                _logger.LogInformation("Property created with id {PropertyId} by user {UserId}",
                    createdProperty.Id, userId);

                return CreatedAtAction(nameof(GetProperty),
                    new { id = createdProperty.Id },
                    MapToDto(createdProperty));
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while creating property");
                throw new BusinessException("Ошибка сохранения в базе данных", "DATABASE_ERROR", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating property");
                throw;
            }
        }

        private PropertyDto MapToDto(Property property)
        {
            return new PropertyDto
            {
                Id = property.Id,
                Title = property.Title,
                Description = property.Description,
                Address = property.Address,
                Price = property.Price,
                Area = property.Area,
                Type = property.Type,
                Rooms = property.Rooms,
                IsAvailable = property.IsAvailable,
                CreatedAt = property.CreatedAt,
                CreatedByUserId = property.CreatedByUserId
            };
        }

        [HttpPut("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> UpdateProperty(int id, [FromBody] UpdatePropertyRequest request)
        {
            // ДИАГНОСТИКА
            Console.WriteLine($"=== PUT DIAGNOSTIC ===");
            Console.WriteLine($"ID: {id}");
            Console.WriteLine($"Request is null: {request == null}");

            if (request == null)
            {
                return BadRequest("Request body cannot be null");
            }

            try
            {
                var property = await _propertyRepository.GetByIdAsync(id);

                if (property == null)
                {
                    return NotFound(new { Message = $"Property with id {id} not found" });
                }

                // Обновляем только переданные поля
                // Для строк проверяем на null/empty, для чисел на специальные значения

                if (!string.IsNullOrEmpty(request.Title))
                    property.Title = request.Title;

                if (!string.IsNullOrEmpty(request.Description))
                    property.Description = request.Description;

                if (!string.IsNullOrEmpty(request.Address))
                    property.Address = request.Address;

                // Price: 0 означает "не обновлять"
                if (request.Price > 0)
                    property.Price = request.Price;

                // Area: 0 означает "не обновлять"
                if (request.Area > 0)
                    property.Area = request.Area;

                // Type: null/empty означает "не обновлять"
                if (!string.IsNullOrEmpty(request.Type))
                    property.Type = request.Type;

                // Rooms: -1 означает "не обновлять" (по умолчанию)
                if (request.Rooms >= 0)
                    property.Rooms = request.Rooms;

                // IsAvailable: null означает "не обновлять"
                if (request.IsAvailable.HasValue)
                    property.IsAvailable = request.IsAvailable.Value;

                await _propertyRepository.UpdateAsync(property);

                _logger.LogInformation("Property updated with id {PropertyId}", id);

                return Ok(MapToDto(property)); // Возвращаем обновленный объект
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Database error while updating property with id {PropertyId}", id);
                return StatusCode(500, new { Message = "Ошибка сохранения в базе данных", ErrorCode = "DATABASE_ERROR" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating property with id {PropertyId}", id);
                return StatusCode(500, new { Message = "Внутренняя ошибка сервера" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteProperty(int id)
        {
            try
            {
                var property = await _propertyRepository.GetByIdAsync(id);

                if (property == null)
                {
                    throw new NotFoundException(nameof(Property), id);
                }

                // Проверка, можно ли удалять (например, если есть связанные сделки)
                var hasRelatedDeals = await CheckForRelatedDeals(property.Id);
                if (hasRelatedDeals)
                {
                    throw new BusinessException("Невозможно удалить недвижимость, так как с ней связаны сделки",
                        "HAS_RELATED_DEALS");
                }

                await _propertyRepository.DeleteAsync(property);

                _logger.LogInformation("Property deleted with id {PropertyId}", id);

                return NoContent();
            }
            catch (NotFoundException)
            {
                throw;
            }
            catch (BusinessException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting property with id {PropertyId}", id);
                throw;
            }
        }

        [HttpGet("search")]
        public async Task<ActionResult<IEnumerable<PropertyDto>>> SearchProperties(
            [FromQuery] string term,
            [FromQuery] string type)
        {
            try
            {
                var properties = await _propertyRepository.SearchPropertiesAsync(term, type);

                // Map to DTO
                var propertyDtos = properties.Select(p => MapToDto(p)).ToList();

                // Фильтрация для обычных пользователей
                var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (userRole != "Admin")
                {
                    propertyDtos = propertyDtos.Where(p => p.IsAvailable).ToList();
                }

                return Ok(propertyDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching properties with term: {Term}, type: {Type}",
                    term, type);
                throw;
            }
        }

        [HttpGet("available")]
        public async Task<ActionResult<IEnumerable<PropertyDto>>> GetAvailableProperties()
        {
            try
            {
                var properties = await _propertyRepository.GetAvailablePropertiesAsync();
                var propertyDtos = properties.Select(p => MapToDto(p)).ToList();

                return Ok(propertyDtos);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting available properties");
                throw;
            }
        }

        //private PropertyDto MapToDto(Property property)
        //{
        //    return new PropertyDto
        //    {
        //        Id = property.Id,
        //        Title = property.Title,
        //        Description = property.Description,
        //        Address = property.Address,
        //        Price = property.Price,
        //        Area = property.Area,
        //        Type = property.Type,
        //        Rooms = property.Rooms,
        //        IsAvailable = property.IsAvailable,
        //        CreatedAt = property.CreatedAt
        //        // UpdatedAt временно удален, так как его нет в модели Property
        //    };
        //}

        private async Task<bool> CheckForRelatedDeals(int propertyId)
        {
            // В реальном приложении здесь будет запрос к репозиторию сделок
            // Пока возвращаем false для примера
            return await Task.FromResult(false);
        }
    }

    // Классы исключений для лучшей обработки ошибок
    public class BusinessException : Exception
    {
        public string ErrorCode { get; }

        public BusinessException(string message, string errorCode) : base(message)
        {
            ErrorCode = errorCode;
        }

        public BusinessException(string message, string errorCode, Exception innerException)
            : base(message, innerException)
        {
            ErrorCode = errorCode;
        }
    }

    public class NotFoundException : Exception
    {
        public string EntityName { get; }
        public object EntityId { get; }

        public NotFoundException(string entityName, object entityId)
            : base($"{entityName} with id {entityId} not found")
        {
            EntityName = entityName;
            EntityId = entityId;
        }
    }
}