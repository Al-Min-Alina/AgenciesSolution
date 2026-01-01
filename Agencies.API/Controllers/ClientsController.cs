using Agencies.Core.DTO;
using Agencies.Domain.Models;
using Agencies.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public ClientsController(IClientRepository clientRepository, IUserRepository userRepository)
        {
            _clientRepository = clientRepository;
            _userRepository = userRepository;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<ClientDto>>> GetClients([FromQuery] string search = "")
        {
            IEnumerable<Domain.Models.Client> clients; // Явное указание полного имени

            if (!string.IsNullOrWhiteSpace(search))
            {
                clients = await _clientRepository.SearchClientsAsync(search);
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
            }

            var clientDtos = clients.Select(c => MapToDto(c)).ToList();
            return Ok(clientDtos);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ClientDto>> GetClient(int id)
        {
            var client = await _clientRepository.GetByIdAsync(id);

            if (client == null)
            {
                return NotFound();
            }

            // Проверка доступа
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (userRole != "Admin" && client.AgentId != userId)
            {
                return Forbid();
            }

            return Ok(MapToDto(client));
        }

        [HttpPost]
        public async Task<ActionResult<ClientDto>> CreateClient([FromBody] CreateClientRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.FirstName))
            {
                return BadRequest(new { message = "FirstName is required" });
            }

            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;

            // Проверяем, существует ли указанный агент
            if (request.AgentId.HasValue)
            {
                var agent = await _userRepository.GetByIdAsync(request.AgentId.Value);
                if (agent == null)
                {
                    return BadRequest(new { message = "Specified agent does not exist" });
                }

                // Проверяем, что пользователь является админом или создает клиента для себя
                if (userRole != "Admin" && request.AgentId != userId)
                {
                    return Forbid();
                }
            }
            else
            {
                // Если агент не указан, назначаем текущего пользователя
                request.AgentId = userId;
            }

            var client = new Domain.Models.Client // Явное указание полного имени
            {
                FirstName = request.FirstName?.Trim(),
                LastName = request.LastName?.Trim() ?? string.Empty,
                Phone = request.Phone?.Trim() ?? string.Empty,
                Email = request.Email?.Trim() ?? string.Empty,
                Requirements = request.Requirements,
                Budget = request.Budget,
                CreatedAt = DateTime.UtcNow,
                AgentId = request.AgentId.Value // Поскольку мы гарантированно установили значение
            };

            var createdClient = await _clientRepository.AddAsync(client);
            return CreatedAtAction(nameof(GetClient), new { id = createdClient.Id }, MapToDto(createdClient));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateClient(int id, [FromBody] UpdateClientRequest request)
        {
            var client = await _clientRepository.GetByIdAsync(id);

            if (client == null)
            {
                return NotFound();
            }

            // Проверка доступа
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (userRole != "Admin" && client.AgentId != userId)
            {
                return Forbid();
            }

            // Обновляем данные
            client.FirstName = request.FirstName;
            client.LastName = request.LastName;
            client.Phone = request.Phone;
            client.Email = request.Email;
            client.Requirements = request.Requirements;
            client.Budget = request.Budget;

            // Админ может изменить агента
            if (userRole == "Admin" && request.AgentId.HasValue)
            {
                var agent = await _userRepository.GetByIdAsync(request.AgentId.Value);
                if (agent != null)
                {
                    client.AgentId = request.AgentId.Value;
                }
            }

            await _clientRepository.UpdateAsync(client);
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize(Policy = "AdminOnly")]
        public async Task<IActionResult> DeleteClient(int id)
        {
            var client = await _clientRepository.GetByIdAsync(id);

            if (client == null)
            {
                return NotFound();
            }

            // Проверяем, есть ли связанные сделки
            // Убедитесь, что в модели Client есть навигационное свойство Deals
            if (client.Deals != null && client.Deals.Any())
            {
                return BadRequest(new { message = "Cannot delete client with existing deals" });
            }

            await _clientRepository.DeleteAsync(client);
            return NoContent();
        }

        [HttpGet("agent/{agentId}")]
        public async Task<ActionResult<IEnumerable<ClientDto>>> GetClientsByAgent(int agentId)
        {
            // Проверка доступа
            var userRole = User.FindFirst(ClaimTypes.Role)?.Value;
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            if (userRole != "Admin" && agentId != userId)
            {
                return Forbid();
            }

            var clients = await _clientRepository.GetClientsByAgentAsync(agentId);
            var clientDtos = clients.Select(c => MapToDto(c)).ToList();

            return Ok(clientDtos);
        }

        private ClientDto MapToDto(Domain.Models.Client client) // Явное указание типа параметра
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