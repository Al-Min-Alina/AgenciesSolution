using Agencies.Core.DTO;
using Agencies.Domain.Models;
using Agencies.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging; // Добавьте это
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
    public class ClientsController : ControllerBase
    {
        private readonly IClientRepository _clientRepository;
        private readonly IUserRepository _userRepository;
        private readonly ILogger<ClientsController> _logger; // Добавьте поле

        public ClientsController(
            IClientRepository clientRepository,
            IUserRepository userRepository,
            ILogger<ClientsController> logger) // Добавьте параметр
        {
            _clientRepository = clientRepository;
            _userRepository = userRepository;
            _logger = logger; // Инициализируйте
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ClientDto>>> GetClients([FromQuery] string search = "")
        {
            _logger.LogInformation("Запрос списка клиентов. Поиск: {Search}", search ?? "пусто");

            IEnumerable<Domain.Models.Client> clients;

            if (!string.IsNullOrWhiteSpace(search))
            {
                clients = await _clientRepository.SearchClientsAsync(search);
                _logger.LogDebug("Выполнен поиск клиентов по запросу: {Search}", search);
            }
            else
            {
                clients = await _clientRepository.GetAllAsync();
            }

            // Проверяем роль пользователя
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (userRole != "Admin")
            {
                // Пользователи видят только своих клиентов
                clients = clients.Where(c => c.AgentId == userId);
                _logger.LogDebug("Фильтрация клиентов для пользователя ID: {UserId}", userId);
            }

            var clientDtos = clients.Select(c => MapToDto(c)).ToList();
            _logger.LogInformation("Возвращено {Count} клиентов", clientDtos.Count);

            return Ok(clientDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ClientDto>> GetClient(int id)
        {
            _logger.LogInformation("Запрос клиента с ID: {ClientId}", id);

            var client = await _clientRepository.GetByIdAsync(id);

            if (client == null)
            {
                _logger.LogWarning("Клиент с ID: {ClientId} не найден", id);
                return NotFound();
            }

            // Проверка доступа
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (userRole != "Admin" && client.AgentId != userId)
            {
                _logger.LogWarning("Пользователь {UserId} пытался получить доступ к чужому клиенту {ClientId}", userId, id);
                return Forbid();
            }

            _logger.LogDebug("Клиент с ID: {ClientId} успешно получен", id);
            return Ok(MapToDto(client));
        }

        [HttpPost]
        public async Task<ActionResult<ClientDto>> CreateClient([FromBody] CreateClientRequest request)
        {
            _logger.LogInformation("Создание нового клиента. Имя: {FirstName}", request.FirstName);

            if (string.IsNullOrWhiteSpace(request.FirstName))
            {
                _logger.LogWarning("Попытка создания клиента без имени");
                return BadRequest(new { message = "FirstName is required" });
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            _logger.LogDebug("Создание клиента пользователем: {UserId}, роль: {UserRole}", userId, userRole);

            // Проверяем, существует ли указанный агент
            if (request.AgentId.HasValue)
            {
                var agent = await _userRepository.GetByIdAsync(request.AgentId.Value);
                if (agent == null)
                {
                    _logger.LogWarning("Указанный агент не найден: {AgentId}", request.AgentId.Value);
                    return BadRequest(new { message = "Specified agent does not exist" });
                }

                // Проверяем, что пользователь является админом или создает клиента для себя
                if (userRole != "Admin" && request.AgentId != userId)
                {
                    _logger.LogWarning("Пользователь {UserId} пытается создать клиента для другого агента {AgentId}", userId, request.AgentId.Value);
                    return Forbid();
                }
            }
            else
            {
                // Если агент не указан, назначаем текущего пользователя
                request.AgentId = userId;
                _logger.LogDebug("Агент не указан, назначаем текущего пользователя: {UserId}", userId);
            }

            var client = new Domain.Models.Client
            {
                FirstName = request.FirstName?.Trim(),
                LastName = request.LastName?.Trim() ?? string.Empty,
                Phone = request.Phone?.Trim() ?? string.Empty,
                Email = request.Email?.Trim() ?? string.Empty,
                Requirements = request.Requirements,
                Budget = request.Budget,
                CreatedAt = DateTime.UtcNow,
                AgentId = request.AgentId.Value
            };

            try
            {
                var createdClient = await _clientRepository.AddAsync(client);
                _logger.LogInformation("Клиент создан успешно. ID: {ClientId}, Имя: {FirstName}",
                    createdClient.Id, createdClient.FirstName);

                return CreatedAtAction(nameof(GetClient), new { id = createdClient.Id }, MapToDto(createdClient));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при создании клиента. Имя: {FirstName}", request.FirstName);
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ClientDto>> UpdateClient(int id, [FromBody] UpdateClientRequest request)
        {
            _logger.LogInformation("Обновление клиента с ID: {ClientId}", id);

            var client = await _clientRepository.GetByIdAsync(id);

            if (client == null)
            {
                _logger.LogWarning("Клиент с ID: {ClientId} не найден для обновления", id);
                return NotFound();
            }

            // Проверка доступа
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (userRole != "Admin" && client.AgentId != userId)
            {
                _logger.LogWarning("Пользователь {UserId} пытается обновить чужого клиента {ClientId}", userId, id);
                return Forbid();
            }

            // Сохраняем старые значения для логов
            var oldValues = new
            {
                FirstName = client.FirstName,
                LastName = client.LastName,
                Budget = client.Budget,
                AgentId = client.AgentId
            };

            _logger.LogDebug("Старые значения клиента {ClientId}: {OldValues}", id, oldValues);

            // Обновляем данные
            client.FirstName = request.FirstName?.Trim() ?? string.Empty;
            client.LastName = request.LastName?.Trim() ?? string.Empty;
            client.Phone = request.Phone?.Trim() ?? string.Empty;
            client.Email = request.Email?.Trim() ?? string.Empty;
            client.Requirements = request.Requirements;
            client.Budget = request.Budget;

            // Админ может изменить агента
            if (userRole == "Admin" && request.AgentId.HasValue)
            {
                var agent = await _userRepository.GetByIdAsync(request.AgentId.Value);
                if (agent != null)
                {
                    client.AgentId = request.AgentId.Value;
                    _logger.LogDebug("Изменен агент клиента {ClientId} на {NewAgentId}", id, request.AgentId.Value);
                }
            }

            try
            {
                await _clientRepository.UpdateAsync(client);

                var newValues = new
                {
                    FirstName = client.FirstName,
                    LastName = client.LastName,
                    Budget = client.Budget,
                    AgentId = client.AgentId
                };

                _logger.LogInformation("Клиент с ID: {ClientId} успешно обновлен. Изменения: {Changes}",
                    id, new { Old = oldValues, New = newValues });

                // Возвращаем обновленного клиента
                return Ok(MapToDto(client));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при обновлении клиента с ID: {ClientId}. Старые значения: {OldValues}",
                    id, oldValues);

                return StatusCode(500, new
                {
                    message = "Internal server error",
                    error = ex.Message
                });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteClient(int id)
        {
            _logger.LogInformation("Удаление клиента с ID: {ClientId}", id);

            var client = await _clientRepository.GetByIdAsync(id);

            if (client == null)
            {
                _logger.LogWarning("Клиент с ID: {ClientId} не найден для удаления", id);
                return NotFound();
            }

            // Проверяем, есть ли связанные сделки
            if (client.Deals != null && client.Deals.Any())
            {
                _logger.LogWarning("Попытка удалить клиента {ClientId} с существующими сделками. Количество сделок: {DealsCount}",
                    id, client.Deals.Count);
                return BadRequest(new { message = "Cannot delete client with existing deals" });
            }

            try
            {
                await _clientRepository.DeleteAsync(client);
                _logger.LogInformation("Клиент с ID: {ClientId} успешно удален", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при удалении клиента с ID: {ClientId}", id);
                return StatusCode(500, new { message = "Internal server error", error = ex.Message });
            }
        }

        [HttpGet("agent/{agentId}")]
        public async Task<ActionResult<IEnumerable<ClientDto>>> GetClientsByAgent(int agentId)
        {
            _logger.LogInformation("Запрос клиентов агента с ID: {AgentId}", agentId);

            // Проверка доступа
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (userRole != "Admin" && agentId != userId)
            {
                _logger.LogWarning("Пользователь {UserId} пытается получить клиентов другого агента {AgentId}", userId, agentId);
                return Forbid();
            }

            var clients = await _clientRepository.GetClientsByAgentAsync(agentId);
            var clientDtos = clients.Select(c => MapToDto(c)).ToList();

            _logger.LogDebug("Найдено {Count} клиентов для агента {AgentId}", clientDtos.Count, agentId);
            return Ok(clientDtos);
        }

        private ClientDto MapToDto(Domain.Models.Client client)
        {
            if (client == null) return null;

            return new ClientDto
            {
                Id = client.Id,
                FirstName = client.FirstName ?? string.Empty,
                LastName = client.LastName ?? string.Empty,
                Phone = client.Phone ?? string.Empty,
                Email = client.Email ?? string.Empty,
                Requirements = client.Requirements,
                Budget = client.Budget,
                CreatedAt = client.CreatedAt,
                AgentId = client.AgentId,
                AgentName = client.Agent?.Username ?? string.Empty
            };
        }
    }
}